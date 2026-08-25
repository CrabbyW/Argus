namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: the path on the machine's disk an installation lives at
/// (e.g. <c>c:\inetpub\proassistnet</c>).
///
/// Note that the same string can legitimately describe different disks on different machines —
/// the seed data has <c>c:\inetpub\proassistnet.rc0</c> on both BOREAS01 and BOREAS02. Those collapse
/// into one row here, so renaming it renames the path recorded for every machine that uses it.
/// </summary>
public class PhysicalPath : ILookupEntity
{
    public int Id { get; set; }

    /// <summary>Stored in the <c>Path</c> column.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<ApplicationInstallation> Installations { get; set; } =
        new List<ApplicationInstallation>();
}
