using Argus.Api.Services;
using Argus.Api.WebApiPoco.Common;
using Argus.Api.WebApiPoco.Installations;
using log4net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class InstallationsController : ControllerBase
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(InstallationsController));

    private readonly IInstallationService installationService;
    private readonly IEntityJournalService journalService;

    public InstallationsController(
        IInstallationService installationService,
        IEntityJournalService journalService)
    {
        this.installationService = installationService;
        this.journalService = journalService;
    }

    /// <summary>
    /// A read, sent as a POST: the filter travels in the body, not in the query string.
    /// </summary>
    [HttpPost("search")]
    [EndpointName("Installations_SearchInstallations")]
    [ProducesResponseType(typeof(ApiResponse<DataViewOutput<InstallationListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SearchInstallations([FromBody] InstallationFilterDto filter)
    {
        var result = await installationService.GetInstallationsAsync(filter);

        return Ok(new ApiResponse<DataViewOutput<InstallationListItemDto>>
        {
            Success = true,
            Data = result
        });
    }

    [HttpPost("{id:int}/read")]
    [EndpointName("Installations_ReadInstallationById")]
    [ProducesResponseType(typeof(ApiResponse<InstallationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInstallationById(int id)
    {
        var result = await installationService.GetInstallationByIdAsync(id);

        if (result is null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                ErrorCode = "INSTALLATION_NOT_FOUND",
                Message = $"Installation {id} was not found."
            });
        }

        return Ok(new ApiResponse<InstallationDetailDto> { Success = true, Data = result });
    }

    /// <summary>
    /// What was changed on this installation, by whom and when. Lives on the installation rather
    /// than on a journal resource of its own because that is the only way it is ever asked for.
    /// </summary>
    [HttpPost("{id:int}/journal")]
    [EndpointName("Installations_ReadInstallationJournal")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<JournalEntryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInstallationJournal(int id, [FromBody] JournalReadRequestDto? request)
    {
        var entries = await journalService.GetForInstallationAsync(id, request?.MaxEntries ?? 200);

        if (entries is null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                ErrorCode = "INSTALLATION_NOT_FOUND",
                Message = $"Installation {id} was not found."
            });
        }

        return Ok(new ApiResponse<IReadOnlyList<JournalEntryDto>> { Success = true, Data = entries });
    }

    [HttpPost]
    [EndpointName("Installations_CreateInstallation")]
    [ProducesResponseType(typeof(ApiResponse<InstallationDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateInstallation([FromBody] InstallationUpsertDto dto)
    {
        try
        {
            var created = await installationService.CreateInstallationAsync(dto);

            return CreatedAtAction(
                nameof(GetInstallationById),
                new { id = created.Id },
                new ApiResponse<InstallationDetailDto> { Success = true, Data = created });
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"Validation failed creating installation: {ex.Message}");

            return BadRequest(new ErrorResponse
            {
                Success = false,
                ErrorCode = "VALIDATION_ERROR",
                Message = ex.Message
            });
        }
    }

    [HttpPut("{id:int}")]
    [EndpointName("Installations_UpdateInstallation")]
    [ProducesResponseType(typeof(ApiResponse<InstallationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateInstallation(int id, [FromBody] InstallationUpsertDto dto)
    {
        try
        {
            var updated = await installationService.UpdateInstallationAsync(id, dto);

            if (updated is null)
            {
                return NotFound(new ErrorResponse
                {
                    Success = false,
                    ErrorCode = "INSTALLATION_NOT_FOUND",
                    Message = $"Installation {id} was not found."
                });
            }

            return Ok(new ApiResponse<InstallationDetailDto> { Success = true, Data = updated });
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"Validation failed updating installation {id}: {ex.Message}");

            return BadRequest(new ErrorResponse
            {
                Success = false,
                ErrorCode = "VALIDATION_ERROR",
                Message = ex.Message
            });
        }
    }

    [HttpDelete("{id:int}")]
    [EndpointName("Installations_DeleteInstallation")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteInstallation(int id)
    {
        var deleted = await installationService.DeleteInstallationAsync(id);

        if (!deleted)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                ErrorCode = "INSTALLATION_NOT_FOUND",
                Message = $"Installation {id} was not found."
            });
        }

        return Ok(new ApiResponse<bool> { Success = true, Data = true });
    }
}
