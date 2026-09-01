using Argus.Api.WebApiPoco.Auth;
using log4net;

namespace Argus.Api.Services;

/// <summary>
/// Records sign-in attempts in the action log.
///
/// <see cref="Middleware.ActionAuditLoggingMiddleware"/> already writes one line per request, and
/// for a login that line says only that a POST came back 200 — with the password redacted, as it
/// must be. What it cannot say is *how* the session was established, which account behind it, and
/// why an attempt was refused. That is what these lines are for, and they keep the file's shape:
///
///     [timestamp] [action] [command] [status] [actor]
/// </summary>
public interface ILoginAuditLog
{
    void Succeeded(string username, AuthenticationMethod method, LoginContextDto context);

    /// <summary>
    /// <paramref name="attemptedUsername"/> is whatever was tried — it need not exist. The reason
    /// is for the log alone; what goes back to the client stays deliberately vague.
    /// </summary>
    void Failed(string attemptedUsername, AuthenticationMethod method, string reason, LoginContextDto context);

    void SignedOut(string username, AuthenticationMethod method, LoginContextDto context);
}

public class LoginAuditLog : ILoginAuditLog
{
    /// <summary>The audit logger, so sign-ins land in the same file as every other action.</summary>
    private static readonly ILog auditLog = LogManager.GetLogger("ArgusAudit");

    private static readonly ILog logger = LogManager.GetLogger(typeof(LoginAuditLog));

    public void Succeeded(string username, AuthenticationMethod method, LoginContextDto context)
    {
        auditLog.Info(Line("Auth_LoginSucceeded", username, method, context, "200 OK", reason: null));
        logger.Info($"User '{username}' signed in via {method} from {context.IpAddress}.");
    }

    public void Failed(string attemptedUsername, AuthenticationMethod method, string reason, LoginContextDto context)
    {
        auditLog.Info(Line("Auth_LoginFailed", attemptedUsername, method, context, "401 Unauthorized", reason));

        // A failed attempt is a security event, so it is in the diagnostic log too — that is the
        // file that gets shipped somewhere when a run of them needs explaining.
        logger.Warn(
            $"Failed {method} sign-in for '{attemptedUsername}' from {context.IpAddress}: {reason}");
    }

    public void SignedOut(string username, AuthenticationMethod method, LoginContextDto context)
    {
        auditLog.Info(Line("Auth_SignedOut", username, method, context, "200 OK", reason: null));
    }

    /// <summary>
    /// The command field of a sign-in is not a URL — the URL is already on the middleware's own
    /// line for the same request. It is the set of facts that identify the attempt, in
    /// <c>key=value</c> pairs so the file stays greppable (`grep 'method=Windows'`).
    /// </summary>
    private static string Line(
        string action,
        string username,
        AuthenticationMethod method,
        LoginContextDto context,
        string status,
        string? reason)
    {
        var fields = new List<string>
        {
            $"method={method}",
            $"username={Clean(username)}",
            $"windowsAccount={Clean(context.WindowsAccountName) ?? "-"}",
            $"ip={Clean(context.IpAddress) ?? "unknown"}",
            $"userAgent={Clean(context.UserAgent) ?? "unknown"}"
        };

        if (!string.IsNullOrWhiteSpace(reason))
        {
            fields.Add($"reason={Clean(reason)}");
        }

        return $"[{action}] [{string.Join("; ", fields)}] [{status}] [{Clean(username) ?? "anonymous"} ({method})]";
    }

    /// <summary>
    /// Anything reaching this log came off a request, so it can contain newlines and brackets —
    /// either of which would break a reader that splits on them. One record stays one line.
    /// </summary>
    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value
            .Replace("\r", string.Empty)
            .Replace("\n", " ")
            .Replace('[', '(')
            .Replace(']', ')');

        return cleaned.Length > 256 ? cleaned[..256] + "..." : cleaned;
    }
}
