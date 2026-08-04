using Argus.Api.Services;
using Argus.Api.WebApiPoco.Common;
using Argus.Api.WebApiPoco.Logs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Api.Controllers;

/// <summary>
/// Reading the log files from the app itself. Signed in only, and read-only: nothing here
/// writes, deletes or rotates a file — see <see cref="LogFileService"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class LogsController : ControllerBase
{
    private readonly ILogFileService logFiles;

    public LogsController(ILogFileService logFiles)
    {
        this.logFiles = logFiles;
    }

    /// <summary>A read, sent as a POST, like every other read in Argus.</summary>
    [HttpPost("search")]
    [EndpointName("Logs_SearchLogFiles")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LogFileDto>>), StatusCodes.Status200OK)]
    public IActionResult SearchLogFiles([FromBody] ReadRequestBody? request)
    {
        _ = request;

        return Ok(new ApiResponse<IReadOnlyList<LogFileDto>>
        {
            Success = true,
            Data = logFiles.ListFiles()
        });
    }

    [HttpPost("{name}/read")]
    [EndpointName("Logs_ReadLogFile")]
    [ProducesResponseType(typeof(ApiResponse<LogContentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public IActionResult ReadLogFile(string name, [FromBody] LogReadRequestDto? request)
    {
        var content = logFiles.Read(
            name,
            request?.MaxLines ?? 500,
            request?.SearchTerm);

        if (content is null)
        {
            return NotFound(new ErrorResponse
            {
                Success = false,
                ErrorCode = "LOG_FILE_NOT_FOUND",
                Message = $"There is no log file called {name}."
            });
        }

        return Ok(new ApiResponse<LogContentDto> { Success = true, Data = content });
    }
}
