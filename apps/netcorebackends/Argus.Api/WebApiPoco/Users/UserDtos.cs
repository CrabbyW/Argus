using System.ComponentModel.DataAnnotations;
using Argus.Api.WebApiPoco.Common;

namespace Argus.Api.WebApiPoco.Users;

/// <summary>
/// A user as the management screen sees one. Deliberately carries no <c>PasswordHash</c> and no
/// <c>PasswordSalt</c>: the only direction a password ever travels is in.
/// </summary>
public class UserDto
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? LastLoginUtc { get; set; }

    /// <summary>The domain account this user may sign in with, or null for password only.</summary>
    public string? WindowsAccountName { get; set; }

    /// <summary>How the last sign-in happened: "Password", "Windows", or null if never.</summary>
    public string? LastLoginMethod { get; set; }
}

/// <summary>
/// The body of the users read. Its one criterion used to be a query parameter; it travels in the
/// body now, alongside the URL the screen was on.
/// </summary>
public class UserListRequestDto : ReadRequestDto
{
    /// <summary>Include soft-deleted users, which are hidden by default.</summary>
    public bool IncludeDisabled { get; set; }
}

/// <summary>
/// Create and edit share a shape apart from the password: on create it is required, on edit it is
/// not accepted at all. Setting a password is its own endpoint so an edit can never carry one by
/// accident — the screen does a read-modify-PUT, and whatever it read must be safe to send back.
/// </summary>
public class UserUpsertDto
{
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Required on create unless <see cref="WindowsAccountName"/> is given; ignored on update.
    /// </summary>
    [StringLength(256)]
    public string? Password { get; set; }

    /// <summary>
    /// The Windows account that signs in as this user, as the domain reports it
    /// (<c>DOMAIN\samaccount</c>). Empty or null clears the mapping, leaving password sign-in.
    /// Unlike the password this one *is* accepted on update: a mapping is a piece of the user's
    /// identity that an administrator has to be able to correct.
    /// </summary>
    [StringLength(256)]
    public string? WindowsAccountName { get; set; }
}

public class SetPasswordDto
{
    [Required]
    [StringLength(256, MinimumLength = UserPasswordRules.MinimumLength)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// One copy of the length rule. The service enforces it as well as the attributes above, so it
/// holds for any caller, not only for model binding.
/// </summary>
public static class UserPasswordRules
{
    public const int MinimumLength = 8;
}
