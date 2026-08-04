namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: the source-control system an <see cref="AppRepository"/> lives in ("Git", "Svn",
/// "Bitbucket", ...).
///
/// This was a hardcoded C# enum stored as a bare int until 2026-07-31. It is a shared value like
/// any other, so adding a new system was a code change and a deployment, it had no management
/// screen, and nothing stopped an arbitrary number reaching the column. Making it a table is what
/// the rest of the model already does.
/// </summary>
public class RepositoryType : ILookupEntity
{
    public int Id { get; set; }

    /// <summary>Stored in the <c>RepositoryTypeName</c> column.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<AppRepository> AppRepositories { get; set; } = new List<AppRepository>();
}
