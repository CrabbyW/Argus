using Argus.Api.Database.Entities;
using Argus.Api.WebApiPoco.Common;
using Argus.Api.WebApiPoco.Installations;

namespace Argus.Api.Mappers;

/// <summary>Static entity → DTO conversions. No reflection, no mapping library.</summary>
public static class InstallationMapper
{
    public static InstallationListItemDto ToListItemDto(ApplicationInstallation entity) => new()
    {
        Id = entity.Id,
        MachineId = entity.MachineId,
        MachineName = entity.Machine.Name,
        AppNameId = entity.AppNameId,
        AppName = entity.AppName.Name,
        AppStageNameId = entity.AppStageNameId,
        AppStageName = entity.AppStageName.Name,
        ProcessorArchitectureId = entity.ProcessorArchitectureId,
        ProcessorArchitecture = entity.ProcessorArchitecture.Name,
        DnsEndpointId = entity.DnsEndpointId,
        DnsName = entity.DnsEndpoint?.Name,
        RootPathId = entity.RootPathId,
        RootPath = entity.RootPath.Name,
        PhysicalPathId = entity.PhysicalPathId,
        PhysicalPath = entity.PhysicalPath?.Name,
        Tags = entity.InstallationTags
            .Select(link => link.Tag.Name)
            .OrderBy(name => name)
            .ToList(),
        IsActive = entity.IsActive,
        ValidFromDate = entity.ValidFromDate,
        ValidToDate = entity.ValidToDate
    };

    public static InstallationDetailDto ToDetailDto(ApplicationInstallation entity) => new()
    {
        Id = entity.Id,
        MachineId = entity.MachineId,
        MachineName = entity.Machine.Name,
        AppNameId = entity.AppNameId,
        AppName = entity.AppName.Name,
        AppStageNameId = entity.AppStageNameId,
        AppStageName = entity.AppStageName.Name,
        ProcessorArchitectureId = entity.ProcessorArchitectureId,
        ProcessorArchitecture = entity.ProcessorArchitecture.Name,
        DnsEndpointId = entity.DnsEndpointId,
        DnsName = entity.DnsEndpoint?.Name,
        RootPathId = entity.RootPathId,
        RootPath = entity.RootPath.Name,
        PhysicalPathId = entity.PhysicalPathId,
        PhysicalPath = entity.PhysicalPath?.Name,
        Tags = entity.InstallationTags
            .Select(link => new LookupItemDto
            {
                Id = link.TagId,
                Name = link.Tag.Name,
                Description = link.Tag.Description
            })
            .OrderBy(tag => tag.Name)
            .ToList(),
        IsActive = entity.IsActive,
        ValidFromDate = entity.ValidFromDate,
        ValidToDate = entity.ValidToDate,
        CreatedUtc = entity.CreatedUtc,
        ModifiedUtc = entity.ModifiedUtc,
        AppRepositories = entity.InstallationRepositories
            .Select(link => ToAppRepositoryDto(link.AppRepository))
            .OrderBy(repo => repo.RepositoryUrl)
            .ToList()
    };

    public static AppRepositoryDto ToAppRepositoryDto(AppRepository entity) => new()
    {
        Id = entity.Id,
        RepositoryUrl = entity.Name,
        RepositoryTypeId = entity.RepositoryTypeId,
        RepositoryTypeName = entity.RepositoryType?.Name,
        Description = entity.Description,
        InstallationIds = entity.InstallationRepositories
            .Select(link => link.InstallationId)
            .OrderBy(id => id)
            .ToList()
    };

    /// <summary>
    /// Applies an upsert payload onto an entity. Never touches Id or CreatedUtc.
    ///
    /// Tag and repository links are deliberately NOT handled here: diffing them needs the
    /// DbContext to delete link rows, and this mapper is static by design. See
    /// <c>InstallationService.SyncLinks</c>.
    /// </summary>
    public static void ApplyUpsert(ApplicationInstallation entity, InstallationUpsertDto dto)
    {
        entity.MachineId = dto.MachineId;
        entity.AppNameId = dto.AppNameId;
        entity.AppStageNameId = dto.AppStageNameId;
        entity.ProcessorArchitectureId = dto.ProcessorArchitectureId;
        entity.DnsEndpointId = dto.DnsEndpointId;
        entity.RootPathId = dto.RootPathId;
        entity.PhysicalPathId = dto.PhysicalPathId;
        entity.IsActive = dto.IsActive;
        entity.ValidFromDate = dto.ValidFromDate;
        entity.ValidToDate = dto.ValidToDate;
    }
}
