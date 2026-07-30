using Argus.Api.WebApiPoco.Common;
using Argus.Api.WebApiPoco.Installations;

namespace Argus.Api.Services;

public interface IInstallationService
{
    Task<DataViewOutput<InstallationListItemDto>> GetInstallationsAsync(InstallationFilterDto filter);

    Task<InstallationDetailDto?> GetInstallationByIdAsync(int id);

    Task<InstallationDetailDto> CreateInstallationAsync(InstallationUpsertDto dto);

    /// <summary>Returns null when the installation does not exist.</summary>
    Task<InstallationDetailDto?> UpdateInstallationAsync(int id, InstallationUpsertDto dto);

    /// <summary>Soft delete (IsEnabled = 0). Returns false when not found.</summary>
    Task<bool> DeleteInstallationAsync(int id);
}
