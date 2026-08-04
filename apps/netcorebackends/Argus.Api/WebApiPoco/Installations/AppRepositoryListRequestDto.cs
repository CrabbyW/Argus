using Argus.Api.WebApiPoco.Common;

namespace Argus.Api.WebApiPoco.Installations;

/// <summary>
/// The body of the repositories read. Both criteria used to be query parameters; they travel in
/// the body now, alongside the URL the screen was on.
/// </summary>
public class AppRepositoryListRequestDto : ReadRequestDto
{
    /// <summary>Only repositories linked to this installation.</summary>
    public int? InstallationId { get; set; }

    /// <summary>Only repositories of this application.</summary>
    public int? AppNameId { get; set; }
}
