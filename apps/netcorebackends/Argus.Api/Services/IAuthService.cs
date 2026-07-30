using Argus.Api.WebApiPoco.Auth;

namespace Argus.Api.Services;

public interface IAuthService
{
    /// <summary>Returns null when the username is unknown, disabled, or the password is wrong.</summary>
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);

    Task<CurrentUserDto?> GetCurrentUserAsync(string username);
}
