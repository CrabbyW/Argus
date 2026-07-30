namespace Argus.Api.WebApiPoco.Common;

/// <summary>
/// Generic lookup row for Id-backed dropdowns in the UI. The UI submits <see cref="Id"/>,
/// never the name — that is the whole point of the normalized model.
/// </summary>
public class LookupItemDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    // These two mirror LookupUpsertDto. A caller that edits a row sends the whole DTO back,
    // so anything the write side accepts has to be readable here — otherwise an edit silently
    // resets the value it was never told about.

    /// <summary>Only meaningful for AppStages; 0 for every other kind.</summary>
    public int SortOrder { get; set; }

    /// <summary>Only meaningful for DnsEndpoints; false for every other kind.</summary>
    public bool IsLoadBalancer { get; set; }
}
