using System.Security.Claims;
using Argus.Api.Configuration;
using Argus.Api.Services;
using Argus.Api.WebApiPoco.Auth;
using Argus.Api.WebApiPoco.Common;
using log4net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Argus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AuthController : ControllerBase
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(AuthController));

    private readonly IAuthService authService;
    private readonly ILoginAuditLog loginAudit;
    private readonly WindowsAuthOptions windowsAuthOptions;

    public AuthController(
        IAuthService authService,
        ILoginAuditLog loginAudit,
        IOptions<WindowsAuthOptions> windowsAuthOptions)
    {
        this.authService = authService;
        this.loginAudit = loginAudit;
        this.windowsAuthOptions = windowsAuthOptions.Value;
    }

    /// <summary>
    /// What the sign-in screen may offer. Anonymous by necessity — it is read before anyone has
    /// signed in — and says nothing beyond whether the Windows button should be drawn.
    /// </summary>
    [HttpGet("options")]
    [AllowAnonymous]
    [EndpointName("Auth_GetOptions")]
    [ProducesResponseType(typeof(ApiResponse<AuthOptionsDto>), StatusCodes.Status200OK)]
    public IActionResult GetOptions() =>
        Ok(new ApiResponse<AuthOptionsDto>
        {
            Success = true,
            Data = new AuthOptionsDto { WindowsAuthEnabled = windowsAuthOptions.Enabled }
        });

    [HttpPost("login")]
    [AllowAnonymous]
    [EndpointName("Auth_Login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var result = await authService.LoginAsync(request, BuildContext());

        if (result is null)
        {
            // Deliberately vague: do not reveal whether the username exists.
            return Unauthorized(new ErrorResponse
            {
                Success = false,
                ErrorCode = "INVALID_CREDENTIALS",
                Message = "Invalid username or password."
            });
        }

        return Ok(new ApiResponse<LoginResponseDto> { Success = true, Data = result });
    }

    /// <summary>
    /// Signs in with the Windows account the browser negotiated, and hands back the same JWT the
    /// password form does — so exactly one kind of token exists and every other endpoint stays
    /// unaware there is a second way in.
    ///
    /// This is the only endpoint authenticated by Negotiate: the handshake costs a round trip and
    /// needs the browser to trust the site, neither of which is acceptable on every request.
    /// </summary>
    [HttpPost("windows-login")]
    [Authorize(AuthenticationSchemes = NegotiateDefaults.AuthenticationScheme)]
    [EndpointName("Auth_WindowsLogin")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> WindowsLogin()
    {
        if (!windowsAuthOptions.Enabled)
        {
            // The Negotiate scheme is always registered — an endpoint cannot pick its handler at
            // runtime — so the switch is enforced here: with it off, a caller that negotiates
            // successfully is still refused, and the sign-in screen never offers the button.
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse
            {
                Success = false,
                ErrorCode = "WINDOWS_AUTH_DISABLED",
                Message = "Windows authentication is not enabled on this server."
            });
        }

        var account = User.Identity?.Name ?? string.Empty;
        var result = await authService.WindowsLoginAsync(account, BuildContext());

        if (result is null)
        {
            // Unlike the password form there is nothing to keep vague here: the caller has already
            // proved which domain account they are, and being told "your account is not mapped" is
            // the difference between filing a request and retrying forever.
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse
            {
                Success = false,
                ErrorCode = "WINDOWS_ACCOUNT_NOT_MAPPED",
                Message = $"The Windows account '{account}' has no active Argus user. Ask an administrator to map it."
            });
        }

        return Ok(new ApiResponse<LoginResponseDto> { Success = true, Data = result });
    }

    /// <summary>
    /// Ends a session. The token is dropped by the client — nothing server-side to revoke — so
    /// this exists for the log: without it a sign-out leaves no trace at all, and "when did they
    /// leave?" is a question the action log should be able to answer.
    /// </summary>
    [HttpPost("logout")]
    [EndpointName("Auth_Logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        loginAudit.SignedOut(
            User.FindFirstValue(ClaimTypes.Name) ?? "unknown",
            CurrentMethod(),
            BuildContext());

        return NoContent();
    }

    [HttpPost("me")]
    [EndpointName("Auth_GetCurrentUser")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var username = User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized(new ErrorResponse
            {
                Success = false,
                ErrorCode = "INVALID_CREDENTIALS",
                Message = "No authenticated user."
            });
        }

        var user = await authService.GetCurrentUserAsync(username);

        if (user is null)
        {
            // Token is valid but the user was disabled or removed since it was issued.
            logger.Warn($"Valid token for unknown or disabled user '{username}'.");

            return Unauthorized(new ErrorResponse
            {
                Success = false,
                ErrorCode = "INVALID_CREDENTIALS",
                Message = "This account is no longer active."
            });
        }

        // How this session began is a property of the token, not of the row.
        user.AuthenticationMethod = CurrentMethod();

        return Ok(new ApiResponse<CurrentUserDto> { Success = true, Data = user });
    }

    private AuthenticationMethod CurrentMethod() =>
        Enum.TryParse<AuthenticationMethod>(
            User.FindFirstValue(ArgusClaimTypes.AuthenticationMethod),
            out var method)
            ? method
            : AuthenticationMethod.Password;

    /// <summary>
    /// The facts about the caller that only the request carries. Gathered here rather than in the
    /// service, which has no business reading an <c>HttpContext</c>.
    /// </summary>
    private LoginContextDto BuildContext() => new()
    {
        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        UserAgent = Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : "unknown",
        WindowsAccountName = User.Identity?.AuthenticationType == NegotiateDefaults.AuthenticationScheme
            ? User.Identity.Name
            : User.FindFirstValue(ArgusClaimTypes.WindowsAccountName)
    };
}
