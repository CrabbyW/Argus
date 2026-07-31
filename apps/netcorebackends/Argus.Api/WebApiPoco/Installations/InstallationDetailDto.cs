using Argus.Api.WebApiPoco.Common;

namespace Argus.Api.WebApiPoco.Installations;

/// <summary>
/// Full installation for the detail/edit screen. Carries both the Ids (what the form submits)
/// and the resolved names (what the user reads).
/// </summary>
public class InstallationDetailDto
{
    public int Id { get; set; }

    public int MachineId { get; set; }
    public string MachineName { get; set; } = string.Empty;

    public int AppNameId { get; set; }
    public string AppName { get; set; } = string.Empty;

    public int AppStageNameId { get; set; }
    public string AppStageName { get; set; } = string.Empty;

    public int ProcessorArchitectureId { get; set; }
    public string ProcessorArchitecture { get; set; } = string.Empty;

    public int? DnsEndpointId { get; set; }
    public string? DnsName { get; set; }

    public int RootPathId { get; set; }
    public string RootPath { get; set; } = string.Empty;

    public int? PhysicalPathId { get; set; }
    public string? PhysicalPath { get; set; }

    /// <summary>Linked tags, Id + name — the edit form submits the Ids.</summary>
    public IReadOnlyList<LookupItemDto> Tags { get; set; } = Array.Empty<LookupItemDto>();

    public bool IsActive { get; set; }

    public DateOnly ValidFromDate { get; set; }

    public DateOnly? ValidToDate { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }

    /// <summary>Repositories this installation is built from.</summary>
    public IReadOnlyList<AppRepositoryDto> AppRepositories { get; set; } = Array.Empty<AppRepositoryDto>();
}
