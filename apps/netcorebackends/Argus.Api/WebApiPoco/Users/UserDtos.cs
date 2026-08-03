using System.ComponentModel.DataAnnotations;

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

    /// <summary>Required on create, ignored on update.</summary>
    [StringLength(256)]
    public string? Password { get; set; }
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
