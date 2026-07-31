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

    private static AuthService NewService(ArgusDbContext db) =>
        new(db, Microsoft.Extensions.Options.Options.Create(Options));

    private static void AddUser(ArgusDbContext db, string username = "msfadmin", bool isEnabled = true)
    {
        var (hash, salt) = PasswordHasher.HashPassword(Password);

        db.ApplicationUsers.Add(new ApplicationUser
        {
            Username = username,
            DisplayName = "Argus Administrator",
            PasswordHash = hash,
            PasswordSalt = salt,
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
                new LoginRequestDto { Username = "msfadmin", Password = Password });
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
                new LoginRequestDto { Username = "msfadmin", Password = "not the password" });

            Assert.Null(response);
        }
    }

    [Fact]
    public async Task An_unknown_username_is_refused()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        var response = await NewService(db).LoginAsync(
            new LoginRequestDto { Username = "nobody", Password = Password });

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
                new LoginRequestDto { Username = "retired", Password = Password });

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
            await NewService(db).LoginAsync(new LoginRequestDto { Username = "msfadmin", Password = Password });
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
}
