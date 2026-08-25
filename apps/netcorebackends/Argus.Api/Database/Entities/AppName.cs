namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: an application that can be installed (e.g. Helpdesk Portal).
///
/// The property is <c>Name</c> rather than <c>AppName</c> because C# forbids a member with the
/// same name as its enclosing type; the database column is still <c>AppName</c>.
/// </summary>
public class AppName : ILookupEntity
{
    public int Id { get; set; }

    /// <summary>Stored in the <c>AppName</c> column.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    // No AppRepositories collection: a repository is reached through the installations it
    // belongs to, not through the application. Leaving one here would make EF invent a
    // shadow foreign key back to AppNames.

    public ICollection<ApplicationInstallation> Installations { get; set; } =
        new List<ApplicationInstallation>();
}
