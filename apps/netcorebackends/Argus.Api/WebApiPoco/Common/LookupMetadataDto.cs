namespace Argus.Api.WebApiPoco.Common;

/// <summary>
/// Everything the UI needs to render a lookup it has never heard of: what to call it, which
/// optional fields it uses, how long a name may be, and whether it can be written at all.
///
/// This exists so the lookup screen is generic in the same way the server side already is. Before
/// it, adding a lookup meant editing a hand-written tab list, a name-length table and a fixed
/// interface in the frontend — three places that could each be forgotten independently.
/// </summary>
public class LookupMetadataDto
{
    /// <summary>Route segment, e.g. "dnsendpoints". Lower-case for use in a url as-is.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Tab heading, e.g. "DNS endpoints".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Singular for buttons and dialogs, e.g. "DNS endpoint".</summary>
    public string Singular { get; set; } = string.Empty;

    /// <summary>True when the kind is readable here but written through its own endpoint.</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>Width of the name column, read from the EF model.</summary>
    public int MaxNameLength { get; set; }

    public bool HasDescription { get; set; }

    public bool HasSortOrder { get; set; }

    public bool HasLoadBalancer { get; set; }

    /// <summary>
    /// The kind's name format in one line, e.g. "A tag is lower-case letters, digits and
    /// hyphens". Null where names are free text. Shown as the field's hint, so the rule is stated
    /// before a save is refused rather than only after.
    /// </summary>
    public string? NameHint { get; set; }
}
