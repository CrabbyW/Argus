using Argus.Api.Services;
using Argus.Api.WebApiPoco.Common;
using Argus.Api.WebApiPoco.Installations;
using log4net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Api.Controllers;

/// <summary>
/// Source-control locations. A repository is linked to the installations built from it
/// (many-to-many), so the same url is stored once however many deployments share it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AppRepositoriesController : ControllerBase
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(AppRepositoriesController));

    private readonly IAppRepositoryService repositoryService;

    public AppRepositoriesController(IAppRepositoryService repositoryService)
    {
        this.repositoryService = repositoryService;
    }

    /// <summary>
    /// A read, sent as a POST: the criteria travel in the body, not in the query string.
    /// </summary>
    [HttpPost("search")]
    [EndpointName("AppRepositories_SearchRepositories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppRepositoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchRepositories([FromBody] AppRepositoryListRequestDto request)
    {
        var items = await repositoryService.GetAllAsync(request.InstallationId, request.AppNameId);

        return Ok(new ApiResponse<IReadOnlyList<AppRepositoryDto>> { Success = true, Data = items });
    }

    [HttpGet("{id:int}")]
    [EndpointName("AppRepositories_GetRepositoryById")]
    [ProducesResponseType(typeof(ApiResponse<AppRepositoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRepositoryById(int id)
    {
        var item = await repositoryService.GetByIdAsync(id);

        if (item is null)
        {
            return NotFound(NotFoundError(id));
        }

        return Ok(new ApiResponse<AppRepositoryDto> { Success = true, Data = item });
    }

    [HttpPost]
    [EndpointName("AppRepositories_CreateRepository")]
    [ProducesResponseType(typeof(ApiResponse<AppRepositoryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRepository([FromBody] AppRepositoryUpsertDto dto)
    {
        try
        {
            var created = await repositoryService.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetRepositoryById),
                new { id = created.Id },
                new ApiResponse<AppRepositoryDto> { Success = true, Data = created });
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"Validation failed creating repository: {ex.Message}");
            return BadRequest(ValidationError(ex));
        }
    }

    [HttpPut("{id:int}")]
    [EndpointName("AppRepositories_UpdateRepository")]
    [ProducesResponseType(typeof(ApiResponse<AppRepositoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRepository(int id, [FromBody] AppRepositoryUpsertDto dto)
    {
        try
        {
            var updated = await repositoryService.UpdateAsync(id, dto);

            if (updated is null)
            {
                return NotFound(NotFoundError(id));
            }

            return Ok(new ApiResponse<AppRepositoryDto> { Success = true, Data = updated });
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"Validation failed updating repository {id}: {ex.Message}");
            return BadRequest(ValidationError(ex));
        }
    }

    [HttpDelete("{id:int}")]
    [EndpointName("AppRepositories_DeleteRepository")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRepository(int id)
    {
        var deleted = await repositoryService.DeleteAsync(id);

        if (!deleted)
        {
            return NotFound(NotFoundError(id));
        }

        return Ok(new ApiResponse<bool> { Success = true, Data = true });
    }

    private static ErrorResponse NotFoundError(int id) => new()
    {
        Success = false,
        ErrorCode = "APP_REPOSITORY_NOT_FOUND",
        Message = $"Repository {id} was not found."
    };

    private static ErrorResponse ValidationError(ArgumentException ex) => new()
    {
        Success = false,
        ErrorCode = "VALIDATION_ERROR",
        Message = ex.Message
    };
}
