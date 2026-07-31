using Argus.Api.Database.Entities.Enums;

namespace Argus.Api.WebApiPoco.Installations;

public class AppRepositoryDto
{
    public int Id { get; set; }

    public string RepositoryUrl { get; set; } = string.Empty;

    public RepositoryType RepositoryType { get; set; }

    public string? Description { get; set; }

    /// <summary>Installations this repository is linked to.</summary>
    public IReadOnlyList<int> InstallationIds { get; set; } = Array.Empty<int>();
}
