using Argus.Api.Database.Entities.Enums;

namespace Argus.Api.Database.Entities;

/// <summary>
/// A source-control location an <see cref="Application"/>'s code comes from
/// (svn://..., git://..., bitbucket://...). One application may have several.
/// </summary>
public class AppRepository
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public Application Application { get; set; } = null!;

    public string RepositoryUrl { get; set; } = string.Empty;

    public RepositoryType RepositoryType { get; set; } = RepositoryType.Unknown;

    public string? Description { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;
}
