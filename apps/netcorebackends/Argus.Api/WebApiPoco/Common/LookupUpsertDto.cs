using System.ComponentModel.DataAnnotations;

namespace Argus.Api.WebApiPoco.Common;

/// <summary>Create/update payload for the simple lookup management screens.</summary>
public class LookupUpsertDto
{
    /// <summary>
    /// 512 is the widest any lookup allows (PhysicalPaths). The real per-kind limit is read from
    /// the EF model by <c>LookupHandler.MaxNameLength</c>, because one shared DTO cannot express
    /// nine different column widths.
    /// </summary>
    [Required]
    [StringLength(512, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; set; }

    /// <summary>Only used by AppStages; ignored by the other lookups.</summary>
    public int SortOrder { get; set; }

    /// <summary>Only used by DnsEndpoints; ignored by the other lookups.</summary>
    public bool IsLoadBalancer { get; set; }
}
