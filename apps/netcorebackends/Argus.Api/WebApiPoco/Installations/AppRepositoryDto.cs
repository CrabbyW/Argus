using Argus.Api.Database.Entities.Enums;

namespace Argus.Api.WebApiPoco.Installations;

public class AppRepositoryDto
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public string RepositoryUrl { get; set; } = string.Empty;

    public RepositoryType RepositoryType { get; set; }

    public string? Description { get; set; }
}
