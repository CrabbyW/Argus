namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: an application that can be installed (e.g. ProAssistNet).
/// </summary>
public class Application
{
    public int Id { get; set; }

    public string AppName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<AppRepository> AppRepositories { get; set; } = new List<AppRepository>();

    public ICollection<Installation> Installations { get; set; } = new List<Installation>();
}
