using System.ComponentModel.DataAnnotations;

namespace Argus.Api.WebApiPoco.Common;

/// <summary>Create/update payload for the simple lookup management screens.</summary>
public class LookupUpsertDto
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; set; }

    /// <summary>Only used by AppStages; ignored by the other lookups.</summary>
    public int SortOrder { get; set; }

    /// <summary>Only used by DnsEndpoints; ignored by the other lookups.</summary>
    public bool IsLoadBalancer { get; set; }
}
