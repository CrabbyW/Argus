namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: processor architecture an installation is built for (x86, x64, arm, arm64).
/// </summary>
public class ProcessorArchitecture
{
    public int Id { get; set; }

    public string ArchitectureName { get; set; } = string.Empty;

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<Installation> Installations { get; set; } = new List<Installation>();
}
