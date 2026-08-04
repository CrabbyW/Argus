namespace Argus.Api.WebApiPoco.Installations;

public class AppRepositoryDto
{
    public int Id { get; set; }

    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>Null when the source-control system was never recorded.</summary>
    public int? RepositoryTypeId { get; set; }

    /// <summary>
    /// Display name for <see cref="RepositoryTypeId"/>, so a grid does not need a second request
    /// to render a row. The Id remains what a write sends back.
    /// </summary>
    public string? RepositoryTypeName { get; set; }

    public string? Description { get; set; }

    /// <summary>Installations this repository is linked to.</summary>
    public IReadOnlyList<int> InstallationIds { get; set; } = Array.Empty<int>();
}
