namespace Argus.Api.Configuration;

/// <summary>
/// Windows (Negotiate/Kerberos-NTLM) sign-in, offered next to the username + password form.
///
/// Off by default, and deliberately so: Negotiate only works when the API runs on a Windows host
/// inside the domain, and a browser that is asked to negotiate against a server that cannot
/// answer just fails the request. A site that wants it turns it on.
/// </summary>
public class WindowsAuthOptions
{
    public const string SectionName = "WindowsAuth";

    /// <summary>
    /// Whether the Negotiate scheme is registered and <c>/api/auth/windows-login</c> answers.
    /// When false the endpoint returns 404-equivalent "disabled" and the sign-in screen hides
    /// the button, so nothing on the client has to be configured separately.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Create an Argus user the first time an unknown domain account signs in, instead of
    /// refusing it.
    ///
    /// Off by default: on a domain this would make every account that can reach the URL an Argus
    /// user. Sites that treat domain membership as the authorisation turn it on; everyone else
    /// maps accounts by hand on the Users screen.
    /// </summary>
    public bool AutoProvisionUsers { get; set; }
}
