namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: the path within a web site an installation is served from ("/", "/proassistnet.rc0").
/// The same path is used by many installations, so it is stored once and referenced by Id —
/// correcting a typo is then one row, not a search across the whole inventory.
/// </summary>
public class RootPath : ILookupEntity
{
    public int Id { get; set; }

    /// <summary>Stored in the <c>Path</c> column.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<ApplicationInstallation> Installations { get; set; } =
        new List<ApplicationInstallation>();
}
