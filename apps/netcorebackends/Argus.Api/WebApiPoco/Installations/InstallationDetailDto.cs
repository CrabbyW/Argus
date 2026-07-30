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

    public int ApplicationId { get; set; }
    public string AppName { get; set; } = string.Empty;

    public int AppStageId { get; set; }
    public string AppStageName { get; set; } = string.Empty;

    public int ProcessorArchitectureId { get; set; }
    public string ProcessorArchitecture { get; set; } = string.Empty;

    public int? DnsEndpointId { get; set; }
    public string? DnsName { get; set; }

    public string RootPath { get; set; } = string.Empty;

    public string? PhysicalPath { get; set; }

    public string? Tags { get; set; }

    public bool IsActive { get; set; }

    public DateOnly ValidFromDate { get; set; }

    public DateOnly? ValidToDate { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }

    /// <summary>Repositories of the installed application (read-only here).</summary>
    public IReadOnlyList<AppRepositoryDto> AppRepositories { get; set; } = Array.Empty<AppRepositoryDto>();
}
