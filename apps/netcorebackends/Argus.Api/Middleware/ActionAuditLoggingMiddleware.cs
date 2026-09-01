using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Argus.Api.Services;
using log4net;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;

namespace Argus.Api.Middleware;

/// <summary>
/// Writes one line per API request to the action log.
///
/// The point of this log is that it records the *command that was actually sent*, not the
/// fact that something happened. "Installation updated" is useless three weeks later;
/// `PUT /api/installations/14 {"machineId":28,...}` is the answer. Every line has the same
/// five bracketed fields so the file stays greppable and splittable:
///
///     [timestamp] [action] [command] [status] [actor]
///
/// The timestamp is prepended by the log4net layout (see the AuditFile appender), the other
/// four are built here.
///
/// The actor is the signed-in user and the way that session was established — `jnovak (Windows)`
/// — because with two ways into Argus the username alone no longer says how the request came to
/// be trusted. The sign-in itself is recorded in more detail by
/// <see cref="Services.LoginAuditLog"/>, into this same file.
/// </summary>
public class ActionAuditLoggingMiddleware
{
    /// <summary>
    /// Its own logger name, so `log4net.config` can route it to its own file with its own
    /// layout without the diagnostic log's level/thread columns getting in the way.
    /// </summary>
    private static readonly ILog auditLog = LogManager.GetLogger("ArgusAudit");

    /// <summary>
    /// A body is logged in full up to this length. Past it the line is truncated — an audit
    /// file must not be flooded by one oversized payload, and the head of a request is what
    /// identifies it.
    /// </summary>
    private const int MaxBodyChars = 4000;

    /// <summary>
    /// Values that must never reach a log file. Matched on the JSON property name, so a
    /// renamed DTO field only needs adding here rather than being caught by luck.
    /// </summary>
    private static readonly Regex SecretPattern = new(
        "\"(password|newPassword|currentPassword|passwordHash|token|signingKey)\"\\s*:\\s*\"[^\"]*\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly RequestDelegate next;

    public ActionAuditLoggingMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Swagger, static files and anything else outside the API are not actions anyone
        // audits; logging them would bury the rows that matter.
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var command = await BuildCommandAsync(context.Request);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            auditLog.Info(
                $"[{ResolveAction(context)}] [{command}] [{DescribeStatus(context)}] [{DescribeActor(context)}]");
        }
    }

    /// <summary>
    /// The action name is the endpoint name the controller already declares
    /// (`[EndpointName("Installations_CreateInstallation")]`), which is the name the rest of
    /// the system knows the operation by. If routing found nothing — an unknown path, a
    /// request rejected before it matched — the method and path are the honest fallback.
    /// </summary>
    private static string ResolveAction(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var name = endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return $"{context.Request.Method} {context.Request.Path}".Trim();
    }

    /// <summary>
    /// The request exactly as the client sent it: method, full URL and body. This is the
    /// "command" field — reproducible by hand, not a summary of one.
    ///
    /// The whole URL, not just the path: a bare `/api/installations` does not say which host or
    /// scheme served it, and the line is meant to be pasteable into curl as it stands.
    /// </summary>
    private static async Task<string> BuildCommandAsync(HttpRequest request)
    {
        var command = new StringBuilder()
            .Append(request.Method)
            .Append(' ')
            .Append(request.GetDisplayUrl());

        var body = await ReadBodyAsync(request);

        if (!string.IsNullOrWhiteSpace(body))
        {
            command.Append(' ').Append(body);
        }

        return Flatten(command.ToString());
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        // GET and DELETE carry their whole command in the URL; nothing to add.
        if (request.ContentLength is null or 0 || HttpMethods.IsGet(request.Method))
        {
            return string.Empty;
        }

        // The body is a forward-only stream and model binding has not run yet, so it must be
        // buffered before it is read or the controller would receive an empty payload.
        request.EnableBuffering();

        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        body = SecretPattern.Replace(body, match => $"\"{PropertyNameOf(match.Value)}\":\"***\"");

        return body.Length > MaxBodyChars
            ? string.Concat(body.AsSpan(0, MaxBodyChars), "...[truncated]")
            : body;
    }

    private static string PropertyNameOf(string matched) => matched.Split('"')[1];

    /// <summary>
    /// One action is one line. Newlines in a pretty-printed body would otherwise split a
    /// record across lines and break every reader of this file.
    /// </summary>
    private static string Flatten(string value) =>
        value.Replace("\r", string.Empty).Replace("\n", " ");

    /// <summary>
    /// Who the request was authenticated as, and how they signed in. Anonymous requests — the
    /// login call itself among them — say so rather than leaving the field empty; a blank column
    /// in an audit trail reads as a defect.
    /// </summary>
    private static string DescribeActor(HttpContext context)
    {
        var username = context.User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(username))
        {
            return "anonymous";
        }

        var method = context.User.FindFirstValue(ArgusClaimTypes.AuthenticationMethod);

        return string.IsNullOrWhiteSpace(method) ? username : $"{username} ({method})";
    }

    private static string DescribeStatus(HttpContext context)
    {
        var code = context.Response.StatusCode;

        return $"{code} {(HttpStatusCodeText(code))}";
    }

    private static string HttpStatusCodeText(int code) => code switch
    {
        StatusCodes.Status200OK => "OK",
        StatusCodes.Status201Created => "Created",
        StatusCodes.Status204NoContent => "NoContent",
        StatusCodes.Status400BadRequest => "BadRequest",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "NotFound",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status500InternalServerError => "InternalServerError",
        _ => code >= 500 ? "ServerError" : code >= 400 ? "ClientError" : "OK"
    };
}
