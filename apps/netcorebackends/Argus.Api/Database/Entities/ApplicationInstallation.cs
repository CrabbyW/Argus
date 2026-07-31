namespace Argus.Api.Database.Entities;

/// <summary>
/// The core record of Argus and the last table to be filled: one application, at one stage,
/// installed on one machine, at one path. It holds no names of its own — every shared value is
/// referenced by Id into a lookup that must already exist.
/// </summary>
public class ApplicationInstallation
{
    public int Id { get; set; }

    // --- Normalized references (a name is edited in exactly one place) ---

    public int MachineId { get; set; }
    public Machine Machine { get; set; } = null!;

    public int AppNameId { get; set; }
    public AppName AppName { get; set; } = null!;

    public int AppStageNameId { get; set; }
    public AppStageName AppStageName { get; set; } = null!;

    public int ProcessorArchitectureId { get; set; }
    public ProcessorArchitecture ProcessorArchitecture { get; set; } = null!;

    /// <summary>Optional: a background service or console app has no DNS name.</summary>
    public int? DnsEndpointId { get; set; }
    public DnsEndpoint? DnsEndpoint { get; set; }

    public int RootPathId { get; set; }
    public RootPath RootPath { get; set; } = null!;

    /// <summary>Optional: not every installation records where it sits on disk.</summary>
    public int? PhysicalPathId { get; set; }
    public PhysicalPath? PhysicalPath { get; set; }

    // --- Many-to-many: several per installation, each shared with other installations ---

    public ICollection<InstallationTag> InstallationTags { get; set; } = new List<InstallationTag>();

    public ICollection<InstallationRepository> InstallationRepositories { get; set; } =
        new List<InstallationRepository>();

    // --- Values genuinely belonging to this one installation ---

    /// <summary>Business flag: is this deployment currently serving traffic.</summary>
    public bool IsActive { get; set; } = true;

    public DateOnly ValidFromDate { get; set; }

    /// <summary>Null = still valid (no end date).</summary>
    public DateOnly? ValidToDate { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ModifiedUtc { get; set; }
}
