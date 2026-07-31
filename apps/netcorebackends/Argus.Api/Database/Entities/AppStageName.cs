namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: deployment stage of an installation (STAGING, RC0, MAIN, PenTest, Mirror).
/// </summary>
public class AppStageName : ILookupEntity
{
    public int Id { get; set; }

    /// <summary>Stored in the <c>StageName</c> column.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Display order in dropdowns; lower comes first.</summary>
    public int SortOrder { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<ApplicationInstallation> Installations { get; set; } =
        new List<ApplicationInstallation>();
}
