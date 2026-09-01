using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.WebApiPoco.Users;
using log4net;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Services;

/// <summary>
/// CRUD over <see cref="ApplicationUser"/>. Everything here that looks like ceremony is one of the
/// two lockout guards described in <c>ai-implementation-plan/12_user_management.md</c>.
/// </summary>
public class UserService : IUserService
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(UserService));

    private readonly ArgusDbContext db;

    public UserService(ArgusDbContext db)
    {
        this.db = db;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(bool includeDisabled)
    {
        var query = db.ApplicationUsers.AsNoTracking();

        if (includeDisabled)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .OrderBy(x => x.Username)
            .Select(Projection)
            .ToListAsync();
    }

    public async Task<UserDto?> GetByIdAsync(int id) =>
        await db.ApplicationUsers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.Id == id)
            .Select(Projection)
            .FirstOrDefaultAsync();

    public async Task<UserDto> CreateAsync(UserUpsertDto dto)
    {
        var username = (dto.Username ?? string.Empty).Trim();
        EnsureUsernameFree(await FindByUsernameAsync(username), username);

        var windowsAccount = NormaliseAccount(dto.WindowsAccountName);
        await EnsureWindowsAccountFreeAsync(windowsAccount, exceptUserId: null);

        // A password is required unless a Windows account is mapped — a user with neither cannot
        // sign in at all, and an account nobody can use is a mistake rather than a choice. Given
        // one, a password is still allowed: an administrator may want both ways in.
        string? hash = null;
        string? salt = null;

        if (windowsAccount is null || !string.IsNullOrEmpty(dto.Password))
        {
            EnsurePasswordAcceptable(dto.Password);
            (hash, salt) = PasswordHasher.HashPassword(dto.Password!);
        }

        var user = new ApplicationUser
        {
            Username = username,
            DisplayName = (dto.DisplayName ?? string.Empty).Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            WindowsAccountName = windowsAccount
        };

        db.ApplicationUsers.Add(user);
        await db.SaveChangesAsync();

        logger.Info($"Created user '{user.Username}'.");

        return ToDto(user);
    }

    public async Task<UserDto?> UpdateAsync(int id, UserUpsertDto dto)
    {
        // A disabled user is still editable — renaming one should not require enabling it first.
        var user = await db.ApplicationUsers.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);

        if (user is null)
        {
            return null;
        }

        var username = (dto.Username ?? string.Empty).Trim();
        var clash = await FindByUsernameAsync(username);

        if (clash is not null && clash.Id != id)
        {
            EnsureUsernameFree(clash, username);
        }

        var windowsAccount = NormaliseAccount(dto.WindowsAccountName);
        await EnsureWindowsAccountFreeAsync(windowsAccount, exceptUserId: id);

        // Clearing the mapping on a user who has no password would lock them out of their own
        // account, which is not something an edit should be able to do by omission.
        if (windowsAccount is null && user.PasswordHash is null)
        {
            throw new ArgumentException(
                $"User '{user.Username}' signs in with Windows only. Set a password before removing the Windows account.");
        }

        user.Username = username;
        user.DisplayName = (dto.DisplayName ?? string.Empty).Trim();
        user.WindowsAccountName = windowsAccount;

        // dto.Password is ignored on purpose: passwords are set through SetPasswordAsync only.
        await db.SaveChangesAsync();

        logger.Info($"Updated user {id} ('{user.Username}').");

        return ToDto(user);
    }

    public async Task<bool> SetPasswordAsync(int id, string password)
    {
        var user = await db.ApplicationUsers.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);

        if (user is null)
        {
            return false;
        }

        EnsurePasswordAcceptable(password);

        var (hash, salt) = PasswordHasher.HashPassword(password);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        await db.SaveChangesAsync();

        // The password itself is never logged, here or anywhere else.
        logger.Info($"Password changed for user '{user.Username}'.");

        return true;
    }

    public async Task<bool> DisableAsync(int id, string actingUsername)
    {
        var user = await db.ApplicationUsers.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);

        if (user is null)
        {
            return false;
        }

        if (!user.IsEnabled)
        {
            // Already gone. Idempotent rather than an error.
            return true;
        }

        if (string.Equals(user.Username, actingUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "You cannot disable your own account — it would sign you out with no way back in.");
        }

        var enabledCount = await db.ApplicationUsers.CountAsync();

        if (enabledCount <= 1)
        {
            // DbSeeder will not rescue this: it seeds only when the table is empty, and a
            // soft-deleted row is not empty. Refuse before the door locks, not after.
            throw new ArgumentException(
                "This is the last account that can sign in. Create another user before disabling it.");
        }

        user.IsEnabled = false;
        await db.SaveChangesAsync();

        logger.Info($"Disabled user '{user.Username}'.");

        return true;
    }

    public async Task<bool> RestoreAsync(int id)
    {
        var user = await db.ApplicationUsers.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);

        if (user is null)
        {
            return false;
        }

        user.IsEnabled = true;
        await db.SaveChangesAsync();

        logger.Info($"Restored user '{user.Username}'.");

        return true;
    }

    /// <summary>
    /// Looks past the query filter: a disabled user still owns their username, because the unique
    /// index does too. Without this, creating a second 'msfadmin' would pass validation and then
    /// fail as a 500 at <c>SaveChanges</c>.
    /// </summary>
    private Task<ApplicationUser?> FindByUsernameAsync(string username) =>
        db.ApplicationUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Username.ToLower() == username.ToLower());

    private static void EnsureUsernameFree(ApplicationUser? existing, string username)
    {
        if (existing is not null)
        {
            throw new ArgumentException($"Username '{username}' is already taken.");
        }
    }

    /// <summary>
    /// Trimmed, and empty becomes null: the unique index treats every NULL as its own value, but
    /// two empty strings are one duplicate — and "no mapping" is what an emptied field means.
    /// </summary>
    private static string? NormaliseAccount(string? account)
    {
        var trimmed = (account ?? string.Empty).Trim();

        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>
    /// One domain account signs in as one Argus user. Checked here rather than left to the unique
    /// index, so a second mapping is a message instead of a 500.
    /// </summary>
    private async Task EnsureWindowsAccountFreeAsync(string? account, int? exceptUserId)
    {
        if (account is null)
        {
            return;
        }

        var clash = await db.ApplicationUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.WindowsAccountName != null
                && x.WindowsAccountName.ToLower() == account.ToLower());

        if (clash is not null && clash.Id != exceptUserId)
        {
            throw new ArgumentException(
                $"The Windows account '{account}' is already mapped to user '{clash.Username}'.");
        }
    }

    private static void EnsurePasswordAcceptable(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < UserPasswordRules.MinimumLength)
        {
            throw new ArgumentException(
                $"The password must be at least {UserPasswordRules.MinimumLength} characters.");
        }
    }

    private static UserDto ToDto(ApplicationUser user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        DisplayName = user.DisplayName,
        IsEnabled = user.IsEnabled,
        CreatedUtc = user.CreatedUtc,
        LastLoginUtc = user.LastLoginUtc,
        WindowsAccountName = user.WindowsAccountName,
        LastLoginMethod = user.LastLoginMethod
    };

    private static readonly System.Linq.Expressions.Expression<Func<ApplicationUser, UserDto>> Projection =
        x => new UserDto
        {
            Id = x.Id,
            Username = x.Username,
            DisplayName = x.DisplayName,
            IsEnabled = x.IsEnabled,
            CreatedUtc = x.CreatedUtc,
            LastLoginUtc = x.LastLoginUtc,
            WindowsAccountName = x.WindowsAccountName,
            LastLoginMethod = x.LastLoginMethod
        };
}
