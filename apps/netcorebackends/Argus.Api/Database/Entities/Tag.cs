namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: a label applied to installations ("web", "prod", "service"). One installation carries
/// several tags and one tag is used by many installations, so the relationship is many-to-many
/// through <see cref="InstallationTag"/> rather than the delimited string this replaced.
/// </summary>
public class Tag : ILookupEntity
{
    public int Id { get; set; }

    /// <summary>Stored in the <c>TagName</c> column.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<InstallationTag> InstallationTags { get; set; } = new List<InstallationTag>();
}
