using Argus.Api.Services;
using Argus.Api.WebApiPoco.Common;
using log4net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Api.Controllers;

/// <summary>
/// The lookup tables share one shape, so they share one controller. These are the tables that are
/// filled before any installation can be recorded.
///
/// The kinds are not listed here on purpose — <c>GET /api/lookups</c> enumerates them, and that
/// is the list the UI builds itself from. <c>apprepositories</c> is readable only: repositories
/// are written through <c>/api/AppRepositories</c>, because their type and installation links have
/// nowhere to live in the shared lookup payload.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class LookupsController : ControllerBase
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(LookupsController));

    private readonly ILookupService lookupService;

    public LookupsController(ILookupService lookupService)
    {
        this.lookupService = lookupService;
    }

    /// <summary>
    /// Every kind and how to render it. Deliberately the first call the lookup screen makes: the
    /// tabs, the form fields and the name-length limits all come from here rather than from a copy
    /// kept in the frontend.
    /// </summary>
    [HttpGet]
    [EndpointName("Lookups_GetLookupMetadata")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LookupMetadataDto>>), StatusCodes.Status200OK)]
    public IActionResult GetLookupMetadata() =>
        Ok(new ApiResponse<IReadOnlyList<LookupMetadataDto>>
        {
            Success = true,
            Data = lookupService.GetMetadata()
        });

    [HttpGet("{kind}")]
    [EndpointName("Lookups_GetLookupItems")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LookupItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetLookupItems(string kind)
    {
        if (!TryParseKind(kind, out var parsed, out var error))
        {
            return BadRequest(error);
        }

        var items = await lookupService.GetAllAsync(parsed);

        return Ok(new ApiResponse<IReadOnlyList<LookupItemDto>> { Success = true, Data = items });
    }

    [HttpGet("{kind}/{id:int}")]
    [EndpointName("Lookups_GetLookupItemById")]
    [ProducesResponseType(typeof(ApiResponse<LookupItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLookupItemById(string kind, int id)
    {
        if (!TryParseKind(kind, out var parsed, out var error))
        {
            return BadRequest(error);
        }

        var item = await lookupService.GetByIdAsync(parsed, id);

        if (item is null)
        {
            return NotFound(NotFoundError(kind, id));
        }

        return Ok(new ApiResponse<LookupItemDto> { Success = true, Data = item });
    }

    [HttpPost("{kind}")]
    [EndpointName("Lookups_CreateLookupItem")]
    [ProducesResponseType(typeof(ApiResponse<LookupItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLookupItem(string kind, [FromBody] LookupUpsertDto dto)
    {
        if (!TryParseKind(kind, out var parsed, out var error))
        {
            return BadRequest(error);
        }

        try
        {
            var created = await lookupService.CreateAsync(parsed, dto);

            return CreatedAtAction(
                nameof(GetLookupItemById),
                new { kind, id = created.Id },
                new ApiResponse<LookupItemDto> { Success = true, Data = created });
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"Validation failed creating {kind} lookup: {ex.Message}");
            return BadRequest(ValidationError(ex));
        }
        catch (NotSupportedException ex)
        {
            return ReadOnlyKind(kind, ex);
        }
    }

    [HttpPut("{kind}/{id:int}")]
    [EndpointName("Lookups_UpdateLookupItem")]
    [ProducesResponseType(typeof(ApiResponse<LookupItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLookupItem(string kind, int id, [FromBody] LookupUpsertDto dto)
    {
        if (!TryParseKind(kind, out var parsed, out var error))
        {
            return BadRequest(error);
        }

        try
        {
            var updated = await lookupService.UpdateAsync(parsed, id, dto);

            if (updated is null)
            {
                return NotFound(NotFoundError(kind, id));
            }

            return Ok(new ApiResponse<LookupItemDto> { Success = true, Data = updated });
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"Validation failed updating {kind} lookup {id}: {ex.Message}");
            return BadRequest(ValidationError(ex));
        }
        catch (NotSupportedException ex)
        {
            return ReadOnlyKind(kind, ex);
        }
    }

    [HttpDelete("{kind}/{id:int}")]
    [EndpointName("Lookups_DeleteLookupItem")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLookupItem(string kind, int id)
    {
        if (!TryParseKind(kind, out var parsed, out var error))
        {
            return BadRequest(error);
        }

        try
        {
            var deleted = await lookupService.DeleteAsync(parsed, id);

            if (!deleted)
            {
                return NotFound(NotFoundError(kind, id));
            }

            return Ok(new ApiResponse<bool> { Success = true, Data = true });
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"Cannot delete {kind} lookup {id}: {ex.Message}");
            return BadRequest(ValidationError(ex));
        }
        catch (NotSupportedException ex)
        {
            return ReadOnlyKind(kind, ex);
        }
    }

    /// <summary>
    /// A kind that can be read here but not written. 405 rather than 400: the request is
    /// well-formed, the method is simply not available on this resource — and the message says
    /// where it is available, so the caller is not left guessing.
    /// </summary>
    private ObjectResult ReadOnlyKind(string kind, NotSupportedException ex)
    {
        logger.Warn($"Rejected write to read-only lookup '{kind}': {ex.Message}");

        return StatusCode(StatusCodes.Status405MethodNotAllowed, new ErrorResponse
        {
            Success = false,
            ErrorCode = "LOOKUP_READ_ONLY",
            Message = $"{kind} can only be read here. Use /api/AppRepositories to change it."
        });
    }

    private static bool TryParseKind(string kind, out LookupKind parsed, out ErrorResponse error)
    {
        if (Enum.TryParse(kind, ignoreCase: true, out parsed))
        {
            error = null!;
            return true;
        }

        error = new ErrorResponse
        {
            Success = false,
            ErrorCode = "UNKNOWN_LOOKUP",
            Message = $"'{kind}' is not a known lookup. Valid values: {string.Join(", ", Enum.GetNames<LookupKind>())}."
        };

        return false;
    }

    private static ErrorResponse NotFoundError(string kind, int id) => new()
    {
        Success = false,
        ErrorCode = "LOOKUP_ITEM_NOT_FOUND",
        Message = $"{kind} item {id} was not found."
    };

    private static ErrorResponse ValidationError(ArgumentException ex) => new()
    {
        Success = false,
        ErrorCode = "VALIDATION_ERROR",
        Message = ex.Message
    };
}
