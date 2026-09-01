namespace Argus.Api.Database.Entities;

/// <summary>
/// A user who can log into Argus, either with username + password (PBKDF2-SHA256 hash + salt)
/// or with the Windows account mapped in <see cref="WindowsAccountName"/> — or both.
/// </summary>
public class ApplicationUser
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Base64 PBKDF2-SHA256 hash. Plaintext passwords are never stored.
    /// Null for an account that signs in with Windows only — an unusable placeholder hash would
    /// be indistinguishable from a real one that nobody knows the password to.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>Base64 per-user random salt. Null exactly when <see cref="PasswordHash"/> is.</summary>
    public string? PasswordSalt { get; set; }

    /// <summary>
    /// The domain account this user signs in with, as Windows reports it (<c>DOMAIN\samaccount</c>).
    /// Null means password sign-in only. Compared case-insensitively, because Windows does.
    /// </summary>
    public string? WindowsAccountName { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden (cannot log in), 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginUtc { get; set; }

    /// <summary>
    /// How the last sign-in happened ("Password" or "Windows"), so the Users screen can answer
    /// "how does this person actually get in?" without anyone reading the log file.
    /// </summary>
    public string? LastLoginMethod { get; set; }
}
