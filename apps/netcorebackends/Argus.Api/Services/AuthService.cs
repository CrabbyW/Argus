using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Argus.Api.Configuration;
using Argus.Api.Database;
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

    public AuthService(ArgusDbContext db, IOptions<JwtOptions> jwtOptions)
    {
        this.db = db;
        this.jwtOptions = jwtOptions.Value;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
    {
        var user = await db.ApplicationUsers
            .FirstOrDefaultAsync(x => x.Username == request.Username);

        // Same outcome for unknown user and wrong password — do not reveal which.
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            logger.Warn($"Failed login attempt for username '{request.Username}'.");
            return null;
        }

        user.LastLoginUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var expiresUtc = DateTime.UtcNow.AddMinutes(jwtOptions.TokenLifetimeMinutes);
        var token = CreateToken(user.Id, user.Username, user.DisplayName, expiresUtc);

        logger.Info($"User '{user.Username}' logged in.");

        return new LoginResponseDto
        {
            Token = token,
            ExpiresUtc = expiresUtc,
            Username = user.Username,
            DisplayName = user.DisplayName
        };
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
                DisplayName = x.DisplayName
            })
            .FirstOrDefaultAsync();
    }

    private string CreateToken(int userId, string username, string displayName, DateTime expiresUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim("displayName", displayName)
        };

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            expires: expiresUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
