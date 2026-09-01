using Argus.Api.WebApiPoco.Auth;

namespace Argus.Api.Services;

public interface IAuthService
{
    /// <summary>Returns null when the username is unknown, disabled, or the password is wrong.</summary>
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, LoginContextDto context);

    /// <summary>
    /// Signs in the Windows account the server already authenticated, by mapping it to an Argus
    /// user. Returns null when no enabled user is mapped to that account and auto-provisioning is
    /// off — the account is real either way, so the caller must not report it as a bad credential.
    /// </summary>
    Task<LoginResponseDto?> WindowsLoginAsync(string windowsAccountName, LoginContextDto context);

    Task<CurrentUserDto?> GetCurrentUserAsync(string username);
}
