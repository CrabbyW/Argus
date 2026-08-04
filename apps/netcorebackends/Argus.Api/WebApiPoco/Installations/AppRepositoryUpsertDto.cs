using System.ComponentModel.DataAnnotations;

namespace Argus.Api.WebApiPoco.Installations;

/// <summary>
/// Write shape for a source-control location. Everything the read DTO exposes is settable here,
/// so an edit can round-trip without losing a field it was never sent.
/// </summary>
public class AppRepositoryUpsertDto
{
    [Required]
    [StringLength(512, MinimumLength = 1)]
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Installations built from this repository. An empty list leaves it registered but
    /// unattached, which is how a repository is added before its installation exists.
    /// </summary>
    public List<int> InstallationIds { get; set; } = new();

    /// <summary>
    /// Id into the RepositoryTypes lookup. Null is allowed and means "not recorded" — the UI
    /// offers a dropdown, never free text, exactly like every other Id-backed field.
    /// </summary>
    public int? RepositoryTypeId { get; set; }

    [StringLength(512)]
    public string? Description { get; set; }
}
