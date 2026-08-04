using System.Security.Claims;
using Argus.Api.Services;
using Argus.Api.WebApiPoco.Auth;
using Argus.Api.WebApiPoco.Common;
using log4net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AuthController : ControllerBase
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(AuthController));

    private readonly IAuthService authService;

    public AuthController(IAuthService authService)
    {
        this.authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EndpointName("Auth_Login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var result = await authService.LoginAsync(request);

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

        return Ok(new ApiResponse<CurrentUserDto> { Success = true, Data = user });
    }
}
