namespace Argus.Api.Database.Entities;

/// <summary>
/// Link row between an installation and a tag.
///
/// Deliberately has no <c>IsEnabled</c> and no query filter: a link is meaningless without both
/// ends, and it is hard-deleted when the tags of an installation are edited. A filtered link table
/// would hide rows from that diff, which would then re-insert them and violate the primary key.
/// Soft delete lives on the two ends, not on the link.
/// </summary>
public class InstallationTag
{
    public int InstallationId { get; set; }
    public ApplicationInstallation Installation { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
