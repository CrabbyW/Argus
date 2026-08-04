namespace Argus.Api.Database.Entities;

/// <summary>
/// One changed field of one installation: when, who, how and what.
///
/// This is not the same thing as <c>logs/argus-actions.log</c>, and neither replaces the other.
/// The file log is shaped like traffic — one line per HTTP request, expired after
/// <c>AuditLog:RetentionDays</c>. This table is shaped like state: it says how installation 14
/// looked before someone moved it to another machine, it survives log retention, and it can be
/// filtered by installation because it is a table rather than a file.
///
/// Deliberately missing, and it should stay that way:
/// <list type="bullet">
/// <item><c>IsEnabled</c> and a query filter — an audit row is never soft-deleted, and a
/// journal that its own application can hide is not evidence of anything.</item>
/// <item><c>ModifiedUtc</c> — a journal row is written once and never edited.</item>
/// <item>A retention sweep. The file log has one because it is diagnostic noise; this table is
/// the record. A few hundred installations edited by a handful of people produce thousands of
/// rows a year, which SQL Server does not notice. If it ever does grow, the answer is a
/// deliberate archive by an operator, not a background service that deletes evidence.</item>
/// </list>
/// </summary>
public class EntityJournalEntry
{
    public int Id { get; set; }

    /// <summary>
    /// Groups the rows written by one save. Editing a machine and an end date is one action by
    /// one person, and this is what lets the UI show it as one even though it is two rows.
    /// </summary>
    public Guid ChangeSetId { get; set; }

    /// <summary>
    /// The installation the change belongs to — always set, including when the row that actually
    /// changed was a link row edited from the Repositories screen. "At the level of one
    /// installation" is the whole point of this table, so there is no nullable anchor here.
    /// </summary>
    public int InstallationId { get; set; }

    public ApplicationInstallation Installation { get; set; } = null!;

    /// <summary>
    /// Which row changed: <c>ApplicationInstallation</c>, <c>InstallationTag</c> or
    /// <c>InstallationRepository</c>. Kept as text rather than an enum so a value written last
    /// year still reads the same after the code is refactored.
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// <c>Created</c>, <c>Updated</c>, <c>Deleted</c>, <c>LinkAdded</c> or <c>LinkRemoved</c>.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// The field as a person reads it — "Machine", not "MachineId". Null on the summary rows
    /// (<c>Created</c>, <c>Deleted</c>), where the whole row is the subject.
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// The value before the change, as text, <b>resolved at the time it was written</b>. A
    /// foreign key is stored as the name the screen showed that day ("GAIIS1"), not as "3":
    /// an Id means nothing three weeks later, and resolving it at read time would let a later
    /// rename in the lookup silently rewrite history. Null means the value was empty.
    /// </summary>
    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    /// <summary>
    /// The raw foreign key behind <see cref="OldValue"/>, when the field is a reference. Kept
    /// alongside the text so "every installation ever moved onto machine 3" stays answerable
    /// even after that machine has been renamed twice. Null for plain columns.
    /// </summary>
    public int? OldValueId { get; set; }

    public int? NewValueId { get; set; }

    /// <summary>
    /// Username as text, not a foreign key into <c>ApplicationUsers</c>. An account can be
    /// renamed or disabled; the journal has to keep saying who it was at the time.
    /// <c>system</c> for writes with no signed-in user.
    /// </summary>
    public string ChangedBy { get; set; } = string.Empty;

    public DateTime ChangedUtc { get; set; }
}
