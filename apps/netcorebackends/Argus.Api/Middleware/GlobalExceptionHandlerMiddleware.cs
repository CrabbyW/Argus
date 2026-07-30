using System.Runtime.ExceptionServices;
using System.Text.Json;
using Argus.Api.WebApiPoco.Common;
using log4net;
using Microsoft.EntityFrameworkCore;

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
        catch (DbUpdateConcurrencyException ex)
        {
            // Someone else saved the same row first. Not a fault — the client needs to reload
            // and decide, so it must be told apart from a genuine failure.
            await WriteAsync(
                context,
                ex,
                StatusCodes.Status409Conflict,
                "CONCURRENCY_CONFLICT",
                "Someone else changed this record while you were editing it. Reload and try again.");
        }
        catch (DbUpdateException ex)
        {
            // Almost always a unique index the service layer did not pre-check. The user can
            // act on "this already exists"; they can do nothing with a 500.
            await WriteAsync(
                context,
                ex,
                StatusCodes.Status409Conflict,
                "CONSTRAINT_VIOLATION",
                "The change conflicts with a record that already exists.");
        }
        catch (Exception ex)
        {
            await WriteAsync(
                context,
                ex,
                StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR",
                "An unexpected error occurred. Please contact support with the trace id.");
        }
    }

    /// <summary>
    /// The exception detail goes to the log and only the trace id goes to the client.
    /// </summary>
    private static async Task WriteAsync(
        HttpContext context,
        Exception ex,
        int statusCode,
        string errorCode,
        string message)
    {
        var traceId = context.TraceIdentifier;
        var where = $"{context.Request.Method} {context.Request.Path}";

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.Error($"Unhandled exception (traceId={traceId}) on {where}", ex);
        }
        else
        {
            logger.Warn($"{errorCode} (traceId={traceId}) on {where}: {ex.Message}");
        }

        if (context.Response.HasStarted)
        {
            // Too late to rewrite the response; the log entry above is the record. Rethrow
            // through ExceptionDispatchInfo so the original stack trace survives.
            ExceptionDispatchInfo.Capture(ex).Throw();
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = new ErrorResponse
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message,
            TraceId = traceId
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
