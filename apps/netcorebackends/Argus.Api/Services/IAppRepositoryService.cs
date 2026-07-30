using Argus.Api.WebApiPoco.Installations;

namespace Argus.Api.Services;

/// <summary>
/// Source-control locations belong to an Application, not to a single installation —
/// the same repository backs every deployment of that application.
/// </summary>
public interface IAppRepositoryService
{
    /// <summary>All repositories, optionally narrowed to one application.</summary>
    Task<IReadOnlyList<AppRepositoryDto>> GetAllAsync(int? applicationId);

    Task<AppRepositoryDto?> GetByIdAsync(int id);

    Task<AppRepositoryDto> CreateAsync(AppRepositoryUpsertDto dto);

    Task<AppRepositoryDto?> UpdateAsync(int id, AppRepositoryUpsertDto dto);

    /// <summary>Soft delete (<c>IsEnabled = 0</c>), consistent with every other entity.</summary>
    Task<bool> DeleteAsync(int id);
}
