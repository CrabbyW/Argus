namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: deployment stage of an installation (Staging, RC0, Main, PenTest, Mirror).
/// </summary>
public class AppStage
{
    public int Id { get; set; }

    public string StageName { get; set; } = string.Empty;

    /// <summary>Display order in dropdowns; lower comes first.</summary>
    public int SortOrder { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<Installation> Installations { get; set; } = new List<Installation>();
}
