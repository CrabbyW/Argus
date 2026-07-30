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

    public InstallationsController(IInstallationService installationService)
    {
        this.installationService = installationService;
    }

    [HttpGet]
    [EndpointName("Installations_GetInstallations")]
    [ProducesResponseType(typeof(ApiResponse<DataViewOutput<InstallationListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetInstallations([FromQuery] InstallationFilterDto filter)
    {
        var result = await installationService.GetInstallationsAsync(filter);

        return Ok(new ApiResponse<DataViewOutput<InstallationListItemDto>>
        {
            Success = true,
            Data = result
        });
    }

    [HttpGet("{id:int}")]
    [EndpointName("Installations_GetInstallationById")]
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
