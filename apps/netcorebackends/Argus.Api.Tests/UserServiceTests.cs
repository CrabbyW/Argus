using Argus.Api.Configuration;
using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.Services;
using Argus.Api.WebApiPoco.Auth;
using Argus.Api.WebApiPoco.Users;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Tests;

/// <summary>
/// The user table is the one place where a bug locks every human out of Argus and the seeder
/// cannot help — it only fills an empty table, and a soft-deleted row is not empty. Most of what
/// is pinned here is that door staying open.
/// </summary>
public class UserServiceTests
{
    private const string GoodPassword = "msfadmin-demo";

    private static UserService NewService(ArgusDbContext db) => new(db);

    private static AuthService NewAuth(ArgusDbContext db) =>
        new(db, Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "Argus",
            Audience = "ArgusClients",
            SigningKey = "a-test-signing-key-of-at-least-32-characters",
            TokenLifetimeMinutes = 60
        }));

    private static void AddUser(ArgusDbContext db, string username, bool isEnabled = true)
    {
        var (hash, salt) = PasswordHasher.HashPassword(GoodPassword);

        db.ApplicationUsers.Add(new ApplicationUser
        {
            Username = username,
            DisplayName = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            IsEnabled = isEnabled
        });

        db.SaveChanges();
    }

    private static UserUpsertDto Upsert(string username, string? password = GoodPassword) => new()
    {
        Username = username,
        DisplayName = $"{username} display",
        Password = password
    };

    [Fact]
    public async Task A_created_user_can_log_in_with_the_password_that_was_set()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            await NewService(db).CreateAsync(Upsert("novak"));
        }

        LoginResponseDto? login;

        await using (var db = testDb.NewContext())
        {
            login = await NewAuth(db).LoginAsync(
                new LoginRequestDto { Username = "novak", Password = GoodPassword });
        }

        Assert.NotNull(login);
        Assert.Equal("novak", login.Username);
    }

    [Fact]
    public async Task No_endpoint_hands_back_the_hash_or_the_salt()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            await NewService(db).CreateAsync(Upsert("novak"));
        }

        await using (var db = testDb.NewContext())
        {
            var all = await NewService(db).GetAllAsync(includeDisabled: false);
            var one = await NewService(db).GetByIdAsync(all[0].Id);

            // A compile-time assertion as much as a runtime one: UserDto has no such members, so
            // adding them would break this file before it broke production.
            Assert.Single(all);
            Assert.NotNull(one);
            Assert.Equal("novak", one.Username);
            Assert.DoesNotContain(
                typeof(UserDto).GetProperties(),
                p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task A_duplicate_username_is_a_validation_error_not_a_unique_index_crash()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "msfadmin");
        }

        await using (var db = testDb.NewContext())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => NewService(db).CreateAsync(Upsert("MSFADMIN")));

            Assert.Contains("already taken", ex.Message);
        }
    }

    /// <summary>
    /// A disabled user still owns their username, because the unique index still holds it. Without
    /// <c>IgnoreQueryFilters</c> in the clash check this passes validation and fails at
    /// <c>SaveChanges</c> as a 500.
    /// </summary>
    [Fact]
    public async Task A_disabled_user_still_holds_their_username()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "msfadmin");
            AddUser(db, "novak", isEnabled: false);
        }

        await using (var db = testDb.NewContext())
        {
            await Assert.ThrowsAsync<ArgumentException>(() => NewService(db).CreateAsync(Upsert("novak")));
        }
    }

    [Fact]
    public async Task A_short_password_is_refused()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => NewService(db).CreateAsync(Upsert("novak", "short")));

        Assert.Contains("at least", ex.Message);
        Assert.Empty(await db.ApplicationUsers.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Editing_a_user_leaves_the_password_alone()
    {
        using var testDb = TestDb.CreateSeeded();
        int id;
        string hashBefore;

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "novak");
            var user = await db.ApplicationUsers.SingleAsync();
            id = user.Id;
            hashBefore = user.PasswordHash;
        }

        await using (var db = testDb.NewContext())
        {
            // The payload carries a password. Update must ignore it.
            await NewService(db).UpdateAsync(id, Upsert("novakova", "a-completely-different-one"));
        }

        await using (var assert = testDb.NewContext())
        {
            var user = await assert.ApplicationUsers.SingleAsync();

            Assert.Equal("novakova", user.Username);
            Assert.Equal(hashBefore, user.PasswordHash);
        }
    }

    [Fact]
    public async Task Setting_a_password_replaces_the_old_one()
    {
        using var testDb = TestDb.CreateSeeded();
        int id;

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "novak");
            id = (await db.ApplicationUsers.SingleAsync()).Id;
        }

        await using (var db = testDb.NewContext())
        {
            Assert.True(await NewService(db).SetPasswordAsync(id, "a-brand-new-password"));
        }

        await using (var db = testDb.NewContext())
        {
            Assert.Null(await NewAuth(db).LoginAsync(
                new LoginRequestDto { Username = "novak", Password = GoodPassword }));
        }

        await using (var db = testDb.NewContext())
        {
            Assert.NotNull(await NewAuth(db).LoginAsync(
                new LoginRequestDto { Username = "novak", Password = "a-brand-new-password" }));
        }
    }

    [Fact]
    public async Task You_cannot_disable_your_own_account()
    {
        using var testDb = TestDb.CreateSeeded();
        int id;

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "msfadmin");
            AddUser(db, "novak");
            id = await db.ApplicationUsers.Where(x => x.Username == "msfadmin").Select(x => x.Id).SingleAsync();
        }

        await using (var db = testDb.NewContext())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => NewService(db).DisableAsync(id, "msfadmin"));

            Assert.Contains("your own account", ex.Message);
        }
    }

    [Fact]
    public async Task The_last_enabled_user_cannot_be_disabled()
    {
        using var testDb = TestDb.CreateSeeded();
        int id;

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "msfadmin");
            AddUser(db, "novak", isEnabled: false);
            id = await db.ApplicationUsers.Where(x => x.Username == "msfadmin").Select(x => x.Id).SingleAsync();
        }

        await using (var db = testDb.NewContext())
        {
            // Acting as somebody else, so this is the last-user guard rather than the self guard.
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => NewService(db).DisableAsync(id, "someone-else"));

            Assert.Contains("last account", ex.Message);
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.True(await assert.ApplicationUsers.AnyAsync());
        }
    }

    [Fact]
    public async Task A_disabled_user_cannot_log_in_and_a_restored_one_can_again()
    {
        using var testDb = TestDb.CreateSeeded();
        int id;

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "msfadmin");
            AddUser(db, "novak");
            id = await db.ApplicationUsers.Where(x => x.Username == "novak").Select(x => x.Id).SingleAsync();
        }

        await using (var db = testDb.NewContext())
        {
            Assert.True(await NewService(db).DisableAsync(id, "msfadmin"));
        }

        await using (var db = testDb.NewContext())
        {
            Assert.Null(await NewAuth(db).LoginAsync(
                new LoginRequestDto { Username = "novak", Password = GoodPassword }));
        }

        await using (var db = testDb.NewContext())
        {
            Assert.True(await NewService(db).RestoreAsync(id));
        }

        await using (var db = testDb.NewContext())
        {
            Assert.NotNull(await NewAuth(db).LoginAsync(
                new LoginRequestDto { Username = "novak", Password = GoodPassword }));
        }
    }

    [Fact]
    public async Task The_list_hides_disabled_users_unless_they_are_asked_for()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "msfadmin");
            AddUser(db, "novak", isEnabled: false);
        }

        await using (var db = testDb.NewContext())
        {
            Assert.Single(await NewService(db).GetAllAsync(includeDisabled: false));
            Assert.Equal(2, (await NewService(db).GetAllAsync(includeDisabled: true)).Count);
        }
    }
}
