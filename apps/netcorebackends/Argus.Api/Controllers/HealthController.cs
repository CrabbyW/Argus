using Argus.Api.Database;
using Argus.Api.WebApiPoco.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly ArgusDbContext db;

    public HealthController(ArgusDbContext db)
    {
        this.db = db;
    }

    [HttpGet]
    [EndpointName("Health_GetStatus")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetStatus()
    {
        var dbReachable = await db.Database.CanConnectAsync();

        if (!dbReachable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse
            {
                Success = false,
                ErrorCode = "DB_UNREACHABLE",
                Message = "The API is running but cannot reach the database."
            });
        }

        return Ok(new ApiResponse<string> { Success = true, Data = "Healthy" });
    }
}
