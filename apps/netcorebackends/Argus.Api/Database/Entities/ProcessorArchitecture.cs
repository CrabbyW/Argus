namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: processor architecture an installation is built for (x86, x64, arm, arm64).
/// </summary>
public class ProcessorArchitecture : ILookupEntity
{
    public int Id { get; set; }

    /// <summary>Stored in the <c>ArchitectureName</c> column.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<ApplicationInstallation> Installations { get; set; } =
        new List<ApplicationInstallation>();
}
