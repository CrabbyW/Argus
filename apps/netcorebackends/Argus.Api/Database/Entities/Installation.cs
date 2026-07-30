namespace Argus.Api.Database.Entities;

/// <summary>
/// The core record of Argus: one application, at one stage, installed on one machine,
/// at one path. Everything shared with other installations is referenced by Id.
/// </summary>
public class Installation
{
    public int Id { get; set; }

    // --- Normalized references (a name is edited in exactly one place) ---

    public int MachineId { get; set; }
    public Machine Machine { get; set; } = null!;

    public int ApplicationId { get; set; }
    public Application Application { get; set; } = null!;

    public int AppStageId { get; set; }
    public AppStage AppStage { get; set; } = null!;

    public int ProcessorArchitectureId { get; set; }
    public ProcessorArchitecture ProcessorArchitecture { get; set; } = null!;

    /// <summary>Optional: a background service or console app has no DNS name.</summary>
    public int? DnsEndpointId { get; set; }
    public DnsEndpoint? DnsEndpoint { get; set; }

    // --- Values genuinely belonging to this one installation ---

    /// <summary>Path within the web site, e.g. "/" or "/proassistnet.rc0".</summary>
    public string RootPath { get; set; } = "/";

    /// <summary>Path on the machine's disk, e.g. "c:\inetpub\proassistnet".</summary>
    public string? PhysicalPath { get; set; }

    /// <summary>Free-text labels. Promoted to its own table in PHASE2 — see plan 4.</summary>
    public string? Tags { get; set; }

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
