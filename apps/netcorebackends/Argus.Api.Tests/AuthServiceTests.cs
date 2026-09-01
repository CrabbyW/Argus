using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Argus.Api.Configuration;
using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.Services;
using Argus.Api.WebApiPoco.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Argus.Api.Tests;

public class AuthServiceTests
{
    private const string Password = "correct horse battery staple";
    private const string SigningKey = "a-test-signing-key-of-at-least-32-characters";

    private static readonly JwtOptions Options = new()
    {
        Issuer = "Argus",
        Audience = "ArgusClients",
        SigningKey = SigningKey,
        TokenLifetimeMinutes = 480
    };

    /// <summary>The context every attempt in these tests comes from; only the log reads it.</summary>
    private static LoginContextDto Context => new()
    {
        IpAddress = "127.0.0.1",
        UserAgent = "xunit"
    };

    private static AuthService NewService(ArgusDbContext db, WindowsAuthOptions? windowsAuth = null) =>
        new(
            db,
            Microsoft.Extensions.Options.Options.Create(Options),
            Microsoft.Extensions.Options.Options.Create(windowsAuth ?? new WindowsAuthOptions()),
            new LoginAuditLog());

    private static void AddUser(
        ArgusDbContext db,
        string username = "msfadmin",
        bool isEnabled = true,
        string? windowsAccountName = null,
        bool withPassword = true)
    {
        string? hash = null;
        string? salt = null;

        if (withPassword)
        {
            (hash, salt) = PasswordHasher.HashPassword(Password);
        }

        db.ApplicationUsers.Add(new ApplicationUser
        {
            Username = username,
            DisplayName = "Argus Administrator",
            PasswordHash = hash,
            PasswordSalt = salt,
            WindowsAccountName = windowsAccountName,
            IsEnabled = isEnabled
        });

        db.SaveChanges();
    }

    /// <summary>
    /// The validation parameters the API itself uses (`Program.cs`), so the assertion is that a
    /// token this service issues is one the API will actually accept — not merely that a string
    /// came back.
    /// </summary>
    private static ClaimsPrincipal Validate(string token) =>
        new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = Options.Issuer,
                ValidAudience = Options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            },
            out _);

    [Fact]
    public async Task A_correct_password_issues_a_token_the_api_accepts()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db);
        }

        LoginResponseDto? response;

        await using (var db = testDb.NewContext())
        {
            response = await NewService(db).LoginAsync(
                new LoginRequestDto { Username = "msfadmin", Password = Password }, Context);
        }

        Assert.NotNull(response);
        Assert.Equal("msfadmin", response.Username);
        Assert.Equal("Argus Administrator", response.DisplayName);

        var principal = Validate(response.Token);

        Assert.Equal("msfadmin", principal.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("Argus Administrator", principal.FindFirst("displayName")?.Value);

        // Lifetime comes from configuration rather than a hardcoded constant.
        Assert.Equal(
            DateTime.UtcNow.AddMinutes(Options.TokenLifetimeMinutes),
            response.ExpiresUtc,
            TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task A_wrong_password_is_refused()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db);
        }

        await using (var db = testDb.NewContext())
        {
            var response = await NewService(db).LoginAsync(
                new LoginRequestDto { Username = "msfadmin", Password = "not the password" }, Context);

            Assert.Null(response);
        }
    }

    [Fact]
    public async Task An_unknown_username_is_refused()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        var response = await NewService(db).LoginAsync(
            new LoginRequestDto { Username = "nobody", Password = Password }, Context);

        Assert.Null(response);
    }

    /// <summary>
    /// Users are soft-deleted like everything else, and the query filter on
    /// <c>IsEnabled</c> is what stops a disabled account from logging in.
    /// </summary>
    [Fact]
    public async Task A_disabled_user_cannot_log_in()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "retired", isEnabled: false);
        }

        await using (var db = testDb.NewContext())
        {
            var response = await NewService(db).LoginAsync(
                new LoginRequestDto { Username = "retired", Password = Password }, Context);

            Assert.Null(response);
        }
    }

    [Fact]
    public async Task A_successful_login_records_when_it_happened()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db);
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.Null(await assert.ApplicationUsers.Select(x => x.LastLoginUtc).SingleAsync());
        }

        await using (var db = testDb.NewContext())
        {
            await NewService(db).LoginAsync(new LoginRequestDto { Username = "msfadmin", Password = Password }, Context);
        }

        await using (var assert = testDb.NewContext())
        {
            var lastLogin = await assert.ApplicationUsers.Select(x => x.LastLoginUtc).SingleAsync();

            Assert.NotNull(lastLogin);
            Assert.Equal(DateTime.UtcNow, lastLogin.Value, TimeSpan.FromMinutes(1));
        }
    }

    [Fact]
    public async Task The_current_user_is_resolved_by_the_name_on_the_token()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db);
        }

        await using (var db = testDb.NewContext())
        {
            var service = NewService(db);

            var current = await service.GetCurrentUserAsync("msfadmin");

            Assert.NotNull(current);
            Assert.Equal("msfadmin", current.Username);
            Assert.Equal("Argus Administrator", current.DisplayName);

            Assert.Null(await service.GetCurrentUserAsync("nobody"));
        }
    }

    /* ───────────────────── Windows sign-in ───────────────────── */

    [Fact]
    public async Task A_mapped_windows_account_signs_in_as_its_argus_user()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "jnovak", windowsAccountName: @"CORP\jnovak", withPassword: false);
        }

        LoginResponseDto? response;

        await using (var db = testDb.NewContext())
        {
            response = await NewService(db).WindowsLoginAsync(@"CORP\jnovak", Context);
        }

        Assert.NotNull(response);
        Assert.Equal("jnovak", response.Username);
        Assert.Equal(AuthenticationMethod.Windows, response.AuthenticationMethod);
        Assert.Equal(@"CORP\jnovak", response.WindowsAccountName);

        // The token is the same kind the password form issues, and it carries how it was obtained.
        var principal = Validate(response.Token);

        Assert.Equal("jnovak", principal.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("Windows", principal.FindFirst(ArgusClaimTypes.AuthenticationMethod)?.Value);
        Assert.Equal(@"CORP\jnovak", principal.FindFirst(ArgusClaimTypes.WindowsAccountName)?.Value);
    }

    /// <summary>Windows compares account names case-insensitively, so Argus has to as well.</summary>
    [Fact]
    public async Task The_windows_account_is_matched_regardless_of_case()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "jnovak", windowsAccountName: @"CORP\jnovak", withPassword: false);
        }

        await using (var db = testDb.NewContext())
        {
            Assert.NotNull(await NewService(db).WindowsLoginAsync(@"corp\JNovak", Context));
        }
    }

    [Fact]
    public async Task An_unmapped_windows_account_is_refused_when_auto_provisioning_is_off()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        Assert.Null(await NewService(db).WindowsLoginAsync(@"CORP\stranger", Context));
        Assert.False(await db.ApplicationUsers.AnyAsync());
    }

    [Fact]
    public async Task An_unmapped_windows_account_is_provisioned_when_that_is_turned_on()
    {
        using var testDb = TestDb.CreateSeeded();
        var options = new WindowsAuthOptions { Enabled = true, AutoProvisionUsers = true };

        await using (var db = testDb.NewContext())
        {
            var response = await NewService(db, options).WindowsLoginAsync(@"CORP\jnovak", Context);

            Assert.NotNull(response);
            // The domain form is the mapping; the short name is the Argus username.
            Assert.Equal("jnovak", response.Username);
        }

        await using (var assert = testDb.NewContext())
        {
            var user = await assert.ApplicationUsers.SingleAsync();

            Assert.Equal(@"CORP\jnovak", user.WindowsAccountName);
            Assert.Null(user.PasswordHash);
            Assert.Equal("Windows", user.LastLoginMethod);
        }
    }

    /// <summary>
    /// Auto-provisioning must not hand a domain account someone else's existing Argus user just
    /// because the short names match.
    /// </summary>
    [Fact]
    public async Task Auto_provisioning_refuses_a_username_that_is_already_taken()
    {
        using var testDb = TestDb.CreateSeeded();
        var options = new WindowsAuthOptions { Enabled = true, AutoProvisionUsers = true };

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "jnovak");
        }

        await using (var db = testDb.NewContext())
        {
            Assert.Null(await NewService(db, options).WindowsLoginAsync(@"CORP\jnovak", Context));
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.Null(await assert.ApplicationUsers.Select(x => x.WindowsAccountName).SingleAsync());
        }
    }

    [Fact]
    public async Task A_disabled_user_cannot_sign_in_with_windows_either()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "retired", isEnabled: false, windowsAccountName: @"CORP\retired");
        }

        await using (var db = testDb.NewContext())
        {
            Assert.Null(await NewService(db).WindowsLoginAsync(@"CORP\retired", Context));
        }
    }

    /// <summary>
    /// A Windows-only account has no password hash to compare against, and "no password set" must
    /// never be an accepted password.
    /// </summary>
    [Fact]
    public async Task A_windows_only_user_cannot_sign_in_through_the_password_form()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db, "jnovak", windowsAccountName: @"CORP\jnovak", withPassword: false);
        }

        await using (var db = testDb.NewContext())
        {
            var response = await NewService(db).LoginAsync(
                new LoginRequestDto { Username = "jnovak", Password = string.Empty }, Context);

            Assert.Null(response);
        }
    }

    [Fact]
    public async Task A_password_login_is_recorded_as_one()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            AddUser(db);
        }

        await using (var db = testDb.NewContext())
        {
            var response = await NewService(db).LoginAsync(
                new LoginRequestDto { Username = "msfadmin", Password = Password }, Context);

            Assert.NotNull(response);
            Assert.Equal(AuthenticationMethod.Password, response.AuthenticationMethod);
            Assert.Null(response.WindowsAccountName);
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.Equal("Password", await assert.ApplicationUsers.Select(x => x.LastLoginMethod).SingleAsync());
        }
    }
}
