using System.ComponentModel.DataAnnotations;

namespace Argus.Api.WebApiPoco.Auth;

/// <summary>
/// How a session was established. Travels in the token as the <c>authMethod</c> claim and is
/// written to the action log on every request, because "who did this" is only half an answer
/// when there is more than one way in.
/// </summary>
public enum AuthenticationMethod
{
    /// <summary>Username + password against the PBKDF2 hash in the database.</summary>
    Password = 0,

    /// <summary>The Windows account the browser negotiated with, mapped to an Argus user.</summary>
    Windows = 1
}

public class LoginRequestDto
{
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresUtc { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>How this token was obtained — the screen shows it, the log records it.</summary>
    public AuthenticationMethod AuthenticationMethod { get; set; }

    /// <summary>The domain account behind a Windows sign-in; null for a password sign-in.</summary>
    public string? WindowsAccountName { get; set; }
}

public class CurrentUserDto
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The account mapped for Windows sign-in, when there is one.</summary>
    public string? WindowsAccountName { get; set; }

    /// <summary>How the current session signed in, read back off the token's claims.</summary>
    public AuthenticationMethod AuthenticationMethod { get; set; }
}

/// <summary>
/// What the sign-in screen needs to know before it draws itself: whether to offer the Windows
/// button at all. Anonymous, and deliberately says nothing else about the server's setup.
/// </summary>
public class AuthOptionsDto
{
    public bool WindowsAuthEnabled { get; set; }
}

/// <summary>
/// Everything about a sign-in attempt that is worth having three weeks later, gathered by the
/// controller because only it can see the request.
/// </summary>
public class LoginContextDto
{
    /// <summary>Remote IP as the server saw it, or "unknown".</summary>
    public string IpAddress { get; set; } = "unknown";

    public string UserAgent { get; set; } = "unknown";

    /// <summary>The negotiated Windows account, on a Windows sign-in only.</summary>
    public string? WindowsAccountName { get; set; }
}
