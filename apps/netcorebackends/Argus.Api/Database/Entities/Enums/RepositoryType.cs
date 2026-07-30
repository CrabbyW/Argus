namespace Argus.Api.Database.Entities.Enums;

/// <summary>
/// Source-control system an <see cref="AppRepository"/> lives in.
/// </summary>
public enum RepositoryType
{
    Unknown = 0,
    Git = 1,
    Svn = 2,
    Bitbucket = 3,
    Mercurial = 4,
    Tfs = 5
}
