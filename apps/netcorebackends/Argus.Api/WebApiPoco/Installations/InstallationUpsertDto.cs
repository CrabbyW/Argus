using System.ComponentModel.DataAnnotations;

namespace Argus.Api.WebApiPoco.Installations;

/// <summary>
/// Create/update payload. Only Ids are accepted for the lookups — a client can never
/// invent a machine or app name here; it must exist in its lookup table first.
/// </summary>
public class InstallationUpsertDto
{
    [Range(1, int.MaxValue, ErrorMessage = "MachineId is required.")]
    public int MachineId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "ApplicationId is required.")]
    public int ApplicationId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "AppStageId is required.")]
    public int AppStageId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "ProcessorArchitectureId is required.")]
    public int ProcessorArchitectureId { get; set; }

    /// <summary>Null is allowed: not every installation has a public endpoint.</summary>
    public int? DnsEndpointId { get; set; }

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string RootPath { get; set; } = "/";

    [StringLength(512)]
    public string? PhysicalPath { get; set; }

    [StringLength(512)]
    public string? Tags { get; set; }

    public bool IsActive { get; set; } = true;

    public DateOnly ValidFromDate { get; set; }

    public DateOnly? ValidToDate { get; set; }
}
