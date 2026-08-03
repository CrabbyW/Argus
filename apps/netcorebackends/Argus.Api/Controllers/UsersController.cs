using System.Security.Claims;
using Argus.Api.Services;
using Argus.Api.WebApiPoco.Common;
using Argus.Api.WebApiPoco.Users;
using log4net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Api.Controllers;

/// <summary>
/// Who can sign in to Argus. Users are not registered as a lookup kind even though the table looks
/// like one — a lookup's payload has nowhere safe to put a password, and the guards a user needs
/// ("not yourself", "not the last one") have nothing to do with whether an installation references
/// the row. See <c>ai-implementation-plan/12_user_management.md</c> §0.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class UsersController : ControllerBase
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(UsersController));

    private readonly IUserService userService;

    public UsersController(IUserService userService)
    {
        this.userService = userService;
    }

    [HttpGet]
    [EndpointName("Users_GetUsers")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] bool includeDisabled = false)
    {
        var users = await userService.GetAllAsync(includeDisabled);

        return Ok(new ApiResponse<IReadOnlyList<UserDto>> { Success = true, Data = users });
    }

    [HttpGet("{id:int}")]
    [EndpointName("Users_GetUserById")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(int id)
    {
        var user = await userService.GetByIdAsync(id);

        if (user is null)
        {
            return NotFound(NotFoundError(id));
        }

        return Ok(new ApiResponse<UserDto> { Success = true, Data = user });
    }

    [HttpPost]
    [EndpointName("Users_CreateUser")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] UserUpsertDto dto)
    {
        try
        {
            var created = await userService.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetUserById),
                new { id = created.Id },
                new ApiResponse<UserDto> { Success = true, Data = created });
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"Validation failed creating user: {ex.Message}");
            return BadRequest(ValidationError(ex));
        }
    }

    [HttpPut("{id:int}")]
    [EndpointName("Users_UpdateUser")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UserUpsertDto dto)
    {
        try
        {
            var updated = await userService.UpdateAsync(id, dto);

            if (updated is null)
            {
                return NotFound(NotFoundError(id));
            }

            return Ok(new ApiResponse<UserDto> { Success = true, Data = updated });
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"Validation failed updating user {id}: {ex.Message}");
            return BadRequest(ValidationError(ex));
        }
    }

    [HttpPost("{id:int}/password")]
    [EndpointName("Users_SetPassword")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPassword(int id, [FromBody] SetPasswordDto dto)
    {
        try
        {
            var changed = await userService.SetPasswordAsync(id, dto.Password);

            if (!changed)
            {
                return NotFound(NotFoundError(id));
            }

            return Ok(new ApiResponse<bool> { Success = true, Data = true });
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"Rejected password change for user {id}: {ex.Message}");
            return BadRequest(ValidationError(ex));
        }
    }

    [HttpDelete("{id:int}")]
    [EndpointName("Users_DisableUser")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableUser(int id)
    {
        try
        {
            var disabled = await userService.DisableAsync(id, User.FindFirstValue(ClaimTypes.Name) ?? string.Empty);

            if (!disabled)
            {
                return NotFound(NotFoundError(id));
            }

            return Ok(new ApiResponse<bool> { Success = true, Data = true });
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"Refused to disable user {id}: {ex.Message}");
            return BadRequest(ValidationError(ex));
        }
    }

    [HttpPost("{id:int}/restore")]
    [EndpointName("Users_RestoreUser")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreUser(int id)
    {
        var restored = await userService.RestoreAsync(id);

        if (!restored)
        {
            return NotFound(NotFoundError(id));
        }

        return Ok(new ApiResponse<bool> { Success = true, Data = true });
    }

    private static ErrorResponse NotFoundError(int id) => new()
    {
        Success = false,
        ErrorCode = "USER_NOT_FOUND",
        Message = $"User {id} was not found."
    };

    private static ErrorResponse ValidationError(ArgumentException ex) => new()
    {
        Success = false,
        ErrorCode = "VALIDATION_ERROR",
        Message = ex.Message
    };
}
