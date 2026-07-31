using System.ComponentModel.DataAnnotations;

namespace Argus.Api.WebApiPoco.Installations;

/// <summary>
/// Create/update payload. Only Ids are accepted for the lookups — a client can never
/// invent a machine or app name here; it must exist in its lookup table first. That is what
/// makes ApplicationInstallations the last table to be filled.
/// </summary>
public class InstallationUpsertDto
{
    [Range(1, int.MaxValue, ErrorMessage = "MachineId is required.")]
    public int MachineId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "AppNameId is required.")]
    public int AppNameId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "AppStageNameId is required.")]
    public int AppStageNameId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "ProcessorArchitectureId is required.")]
    public int ProcessorArchitectureId { get; set; }

    /// <summary>Null is allowed: not every installation has a public endpoint.</summary>
    public int? DnsEndpointId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "RootPathId is required.")]
    public int RootPathId { get; set; }

    /// <summary>Null is allowed: not every installation records a path on disk.</summary>
    public int? PhysicalPathId { get; set; }

    /// <summary>Tags to link. An empty list clears them. Duplicates are ignored.</summary>
    public List<int> TagIds { get; set; } = new();

    /// <summary>Repositories to link. Same rules as <see cref="TagIds"/>.</summary>
    public List<int> RepositoryIds { get; set; } = new();

    public bool IsActive { get; set; } = true;

    public DateOnly ValidFromDate { get; set; }

    public DateOnly? ValidToDate { get; set; }
}
