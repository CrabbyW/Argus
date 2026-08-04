using System.Security.Claims;

namespace Argus.Api.Services;

/// <summary>Who is making the current request, for code that runs below the controller.</summary>
public interface ICurrentUserAccessor
{
    /// <summary>The signed-in username, or <c>system</c> when there is no user.</summary>
    string Username { get; }
}

/// <summary>
/// Reads the username off the request's own claims.
///
/// This is a deliberate exception, not a new convention. Everywhere else in Argus the controller
/// reads the claim and passes it down as an argument — see
/// <c>UsersController.DisableUser</c> — and that stays the rule for service-level writes. It does
/// not work for the journal, which is captured underneath the services in
/// <see cref="Database.Interceptors.EntityJournalInterceptor"/>: threading a username there would
/// mean a new parameter on every write method of two services and a constructor change that every
/// existing test would have to be rewritten around, all to deliver one string the request already
/// carries.
/// </summary>
public class CurrentUserAccessor : ICurrentUserAccessor
{
    /// <summary>
    /// The actor recorded when nothing is signed in — a background service, a data fix, a test.
    /// Never an empty string: a blank column in an audit trail looks like a defect, and the
    /// reader cannot tell it apart from one.
    /// </summary>
    public const string SystemActor = "system";

    private readonly IHttpContextAccessor httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public string Username
    {
        get
        {
            var name = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name);

            return string.IsNullOrWhiteSpace(name) ? SystemActor : name;
        }
    }
}
