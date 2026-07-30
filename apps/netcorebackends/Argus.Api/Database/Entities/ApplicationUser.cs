namespace Argus.Api.Database.Entities;

/// <summary>
/// A user who can log into Argus with username + password (PBKDF2-SHA256 hash + salt).
/// </summary>
public class ApplicationUser
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Base64 PBKDF2-SHA256 hash. Plaintext passwords are never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Base64 per-user random salt.</summary>
    public string PasswordSalt { get; set; } = string.Empty;

    /// <summary>Soft-delete flag: 0 = hidden (cannot log in), 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginUtc { get; set; }
}
