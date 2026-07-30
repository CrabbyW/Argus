using System.Text.Json;
using Argus.Api.WebApiPoco.Common;
using log4net;

namespace Argus.Api.Middleware;

/// <summary>
/// Catches every unhandled exception and turns it into the standard
/// <see cref="ErrorResponse"/>. Controllers therefore only try/catch business validation.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(GlobalExceptionHandlerMiddleware));

    private readonly RequestDelegate next;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            logger.Error($"Unhandled exception (traceId={traceId}) on {context.Request.Method} {context.Request.Path}", ex);

            if (context.Response.HasStarted)
            {
                // Too late to rewrite the response; the log entry above is the record.
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var payload = new ErrorResponse
            {
                Success = false,
                ErrorCode = "INTERNAL_ERROR",
                // Never leak exception details to the client; traceId ties it to the log.
                Message = "An unexpected error occurred. Please contact support with the trace id.",
                TraceId = traceId
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
