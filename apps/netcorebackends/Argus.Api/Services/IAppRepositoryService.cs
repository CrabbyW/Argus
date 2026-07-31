using Argus.Api.WebApiPoco.Installations;

namespace Argus.Api.Services;

/// <summary>
/// Source-control locations are linked to the installations built from them (many-to-many),
/// so one url is stored once no matter how many deployments share it.
/// </summary>
public interface IAppRepositoryService
{
    /// <summary>
    /// All repositories, optionally narrowed to one installation, or to every installation of
    /// one application — the cross-installation view the Repositories screen offers.
    /// </summary>
    Task<IReadOnlyList<AppRepositoryDto>> GetAllAsync(int? installationId, int? appNameId);

    Task<AppRepositoryDto?> GetByIdAsync(int id);

    Task<AppRepositoryDto> CreateAsync(AppRepositoryUpsertDto dto);

    Task<AppRepositoryDto?> UpdateAsync(int id, AppRepositoryUpsertDto dto);

    /// <summary>Soft delete (<c>IsEnabled = 0</c>), consistent with every other entity.</summary>
    Task<bool> DeleteAsync(int id);
}
