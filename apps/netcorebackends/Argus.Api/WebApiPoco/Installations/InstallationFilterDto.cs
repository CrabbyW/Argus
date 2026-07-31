using Argus.Api.WebApiPoco.Common;

namespace Argus.Api.WebApiPoco.Installations;

/// <summary>
/// Grid filter. Inherits paging/sorting/search from <see cref="DataViewFilterBase{T}"/>
/// and adds the Id-based facets the installations screen offers — one per lookup.
/// </summary>
public class InstallationFilterDto : DataViewFilterBase<InstallationListItemDto>
{
    public int? MachineId { get; set; }

    public int? AppNameId { get; set; }

    public int? AppStageNameId { get; set; }

    public int? ProcessorArchitectureId { get; set; }

    public int? DnsEndpointId { get; set; }

    public int? RootPathId { get; set; }

    public int? PhysicalPathId { get; set; }

    /// <summary>
    /// Single tag, not a list: every other facet is one Id, and a list would need array
    /// query-string binding plus an AND/OR decision nobody has made. Multi-select is a
    /// clean follow-up if it is ever wanted.
    /// </summary>
    public int? TagId { get; set; }

    /// <summary>Single repository, for the same reason as <see cref="TagId"/>.</summary>
    public int? RepositoryId { get; set; }

    public bool? IsActive { get; set; }

    /// <summary>
    /// Start of the period of interest. Combined with <see cref="ValidTo"/> this asks
    /// "what was installed during this window?" — an installation matches when its own
    /// validity window overlaps the requested one, not merely when it starts inside it.
    /// Either bound may be supplied on its own (open-ended range).
    /// </summary>
    public DateOnly? ValidFrom { get; set; }

    /// <summary>End of the period of interest. See <see cref="ValidFrom"/>.</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>
    /// Include soft-deleted (IsEnabled = 0) rows. Off by default so the normal grid stays
    /// clean; needed for historical questions, because decommissioning an installation is
    /// a soft delete and the row would otherwise be invisible to a past-date query.
    /// </summary>
    public bool IncludeDisabled { get; set; }
}
