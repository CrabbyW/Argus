using System.ComponentModel.DataAnnotations;
using Argus.Api.Database.Entities.Enums;

namespace Argus.Api.WebApiPoco.Installations;

/// <summary>
/// Write shape for a source-control location. Everything the read DTO exposes is settable here,
/// so an edit can round-trip without losing a field it was never sent.
/// </summary>
public class AppRepositoryUpsertDto
{
    [Required]
    public int ApplicationId { get; set; }

    [Required]
    [StringLength(1024, MinimumLength = 1)]
    public string RepositoryUrl { get; set; } = string.Empty;

    public RepositoryType RepositoryType { get; set; } = RepositoryType.Unknown;

    [StringLength(512)]
    public string? Description { get; set; }
}
