using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Argus.Api.Configuration;
using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.WebApiPoco.Auth;
using log4net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Argus.Api.Services;

public class AuthService : IAuthService
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(AuthService));

    private readonly ArgusDbContext db;
    private readonly JwtOptions jwtOptions;
    private readonly WindowsAuthOptions windowsAuthOptions;
    private readonly ILoginAuditLog loginAudit;

    public AuthService(
        ArgusDbContext db,
        IOptions<JwtOptions> jwtOptions,
        IOptions<WindowsAuthOptions> windowsAuthOptions,
        ILoginAuditLog loginAudit)
    {
        this.db = db;
        this.jwtOptions = jwtOptions.Value;
        this.windowsAuthOptions = windowsAuthOptions.Value;
        this.loginAudit = loginAudit;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, LoginContextDto context)
    {
        var user = await db.ApplicationUsers
            .FirstOrDefaultAsync(x => x.Username == request.Username);

        // Same outcome for unknown user, wrong password and a Windows-only account — the client
        // is told none of it. The log below is where the difference is kept.
        if (user is null)
        {
            loginAudit.Failed(request.Username, AuthenticationMethod.Password, "unknown or disabled user", context);
            return null;
        }

        if (user.PasswordHash is null || user.PasswordSalt is null)
        {
            loginAudit.Failed(
                request.Username,
                AuthenticationMethod.Password,
                "account has no password — Windows sign-in only",
                context);
            return null;
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            loginAudit.Failed(request.Username, AuthenticationMethod.Password, "wrong password", context);
            return null;
        }

        return await IssueTokenAsync(user, AuthenticationMethod.Password, context);
    }

    public async Task<LoginResponseDto?> WindowsLoginAsync(string windowsAccountName, LoginContextDto context)
    {
        var account = (windowsAccountName ?? string.Empty).Trim();
        context.WindowsAccountName = account;

        if (account.Length == 0)
        {
            loginAudit.Failed("(unknown)", AuthenticationMethod.Windows, "no Windows account on the request", context);
            return null;
        }

        // IgnoreQueryFilters so a disabled mapping is found and refused explicitly, rather than
        // silently looking like an unmapped account and being auto-provisioned a second time.
        var user = await db.ApplicationUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.WindowsAccountName != null
                && x.WindowsAccountName.ToLower() == account.ToLower());

        if (user is not null && !user.IsEnabled)
        {
            loginAudit.Failed(user.Username, AuthenticationMethod.Windows, "account is disabled", context);
            return null;
        }

        if (user is null)
        {
            if (!windowsAuthOptions.AutoProvisionUsers)
            {
                loginAudit.Failed(
                    account,
                    AuthenticationMethod.Windows,
                    "no Argus user is mapped to this Windows account",
                    context);
                return null;
            }

            user = await ProvisionAsync(account, context);

            if (user is null)
            {
                return null;
            }
        }

        return await IssueTokenAsync(user, AuthenticationMethod.Windows, context);
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(string username)
    {
        return await db.ApplicationUsers
            .AsNoTracking()
            .Where(x => x.Username == username)
            .Select(x => new CurrentUserDto
            {
                Id = x.Id,
                Username = x.Username,
                DisplayName = x.DisplayName,
                WindowsAccountName = x.WindowsAccountName
            })
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Creates the Argus user for a domain account signing in for the first time, when
    /// <c>WindowsAuth:AutoProvisionUsers</c> allows it. The account name is the username, so the
    /// two never drift apart; no password is set, because there is nothing to set it from.
    /// </summary>
    private async Task<ApplicationUser?> ProvisionAsync(string account, LoginContextDto context)
    {
        var username = ShortNameOf(account);

        // A domain account whose short name is already taken by a password user is not the same
        // person, and quietly handing over that account would be the worst possible guess.
        var clash = await db.ApplicationUsers
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Username.ToLower() == username.ToLower());

        if (clash)
        {
            loginAudit.Failed(
                username,
                AuthenticationMethod.Windows,
                $"cannot auto-provision: username '{username}' is already taken by another account",
                context);
            return null;
        }

        var user = new ApplicationUser
        {
            Username = username,
            DisplayName = account,
            WindowsAccountName = account
        };

        db.ApplicationUsers.Add(user);
        await db.SaveChangesAsync();

        logger.Info($"Auto-provisioned user '{user.Username}' for Windows account '{account}'.");

        return user;
    }

    private async Task<LoginResponseDto> IssueTokenAsync(
        ApplicationUser user,
        AuthenticationMethod method,
        LoginContextDto context)
    {
        user.LastLoginUtc = DateTime.UtcNow;
        user.LastLoginMethod = method.ToString();
        await db.SaveChangesAsync();

        var expiresUtc = DateTime.UtcNow.AddMinutes(jwtOptions.TokenLifetimeMinutes);
        var token = CreateToken(user, method, expiresUtc);

        loginAudit.Succeeded(user.Username, method, context);

        return new LoginResponseDto
        {
            Token = token,
            ExpiresUtc = expiresUtc,
            Username = user.Username,
            DisplayName = user.DisplayName,
            AuthenticationMethod = method,
            WindowsAccountName = method == AuthenticationMethod.Windows ? user.WindowsAccountName : null
        };
    }

    /// <summary>
    /// <c>DOMAIN\jnovak</c> and <c>jnovak@corp.local</c> both become <c>jnovak</c>: the Argus
    /// username is the person, not the form the domain happened to report them in.
    /// </summary>
    private static string ShortNameOf(string account)
    {
        var afterDomain = account.Contains('\\') ? account[(account.IndexOf('\\') + 1)..] : account;
        var beforeRealm = afterDomain.Contains('@') ? afterDomain[..afterDomain.IndexOf('@')] : afterDomain;

        return beforeRealm.Trim();
    }

    private string CreateToken(ApplicationUser user, AuthenticationMethod method, DateTime expiresUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.Username),
            new("displayName", user.DisplayName),
            // Carried in the token so every later request can be logged with the way its session
            // began — the action log answers "how did they get in?" for the whole session, not
            // just for the one request that signed in.
            new(ArgusClaimTypes.AuthenticationMethod, method.ToString())
        };

        if (method == AuthenticationMethod.Windows && user.WindowsAccountName is not null)
        {
            claims.Add(new Claim(ArgusClaimTypes.WindowsAccountName, user.WindowsAccountName));
        }

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            expires: expiresUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>The claims Argus adds of its own, named in one place so nothing is spelled twice.</summary>
public static class ArgusClaimTypes
{
    public const string AuthenticationMethod = "authMethod";

    public const string WindowsAccountName = "windowsAccount";
}
