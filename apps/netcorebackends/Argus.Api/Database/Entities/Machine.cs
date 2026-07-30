namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: a physical or virtual server that hosts installations (e.g. GAIIS1).
/// </summary>
public class Machine
{
    public int Id { get; set; }

    public string MachineName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<Installation> Installations { get; set; } = new List<Installation>();
}
