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
    /// The one facet that takes several values. Tags are the field where asking about more than
    /// one at a time is the normal question ("web or service"), and now that the filter travels
    /// in a POST body a list needs no query-string array binding.
    ///
    /// Matching is OR: an installation matches when it carries <em>any</em> of these tags. AND
    /// would ask a different question — "carries all of them" — and would make each further tag
    /// narrow the result, which is not what picking a second tag in a list looks like.
    /// </summary>
    public List<int> TagIds { get; set; } = new();

    /// <summary>Single repository: one repository is one answer, unlike a set of tags.</summary>
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
