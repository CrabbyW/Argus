namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: a physical or virtual server that hosts installations (e.g. BOREAS01).
/// </summary>
public class Machine : ILookupEntity
{
    public int Id { get; set; }

    /// <summary>Stored in the <c>MachineName</c> column.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<ApplicationInstallation> Installations { get; set; } =
        new List<ApplicationInstallation>();
}
