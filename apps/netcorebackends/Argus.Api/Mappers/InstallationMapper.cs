using Argus.Api.Database.Entities;
using Argus.Api.WebApiPoco.Installations;

namespace Argus.Api.Mappers;

/// <summary>Static entity → DTO conversions. No reflection, no mapping library.</summary>
public static class InstallationMapper
{
    public static InstallationListItemDto ToListItemDto(Installation entity) => new()
    {
        Id = entity.Id,
        MachineName = entity.Machine.MachineName,
        AppName = entity.Application.AppName,
        AppStageName = entity.AppStage.StageName,
        ProcessorArchitecture = entity.ProcessorArchitecture.ArchitectureName,
        DnsName = entity.DnsEndpoint?.DnsName,
        RootPath = entity.RootPath,
        PhysicalPath = entity.PhysicalPath,
        Tags = entity.Tags,
        IsActive = entity.IsActive,
        ValidFromDate = entity.ValidFromDate,
        ValidToDate = entity.ValidToDate
    };

    public static InstallationDetailDto ToDetailDto(Installation entity) => new()
    {
        Id = entity.Id,
        MachineId = entity.MachineId,
        MachineName = entity.Machine.MachineName,
        ApplicationId = entity.ApplicationId,
        AppName = entity.Application.AppName,
        AppStageId = entity.AppStageId,
        AppStageName = entity.AppStage.StageName,
        ProcessorArchitectureId = entity.ProcessorArchitectureId,
        ProcessorArchitecture = entity.ProcessorArchitecture.ArchitectureName,
        DnsEndpointId = entity.DnsEndpointId,
        DnsName = entity.DnsEndpoint?.DnsName,
        RootPath = entity.RootPath,
        PhysicalPath = entity.PhysicalPath,
        Tags = entity.Tags,
        IsActive = entity.IsActive,
        ValidFromDate = entity.ValidFromDate,
        ValidToDate = entity.ValidToDate,
        CreatedUtc = entity.CreatedUtc,
        ModifiedUtc = entity.ModifiedUtc,
        AppRepositories = entity.Application.AppRepositories
            .Select(ToAppRepositoryDto)
            .ToList()
    };

    public static AppRepositoryDto ToAppRepositoryDto(AppRepository entity) => new()
    {
        Id = entity.Id,
        ApplicationId = entity.ApplicationId,
        RepositoryUrl = entity.RepositoryUrl,
        RepositoryType = entity.RepositoryType,
        Description = entity.Description
    };

    /// <summary>Applies an upsert payload onto an entity. Never touches Id or CreatedUtc.</summary>
    public static void ApplyUpsert(Installation entity, InstallationUpsertDto dto)
    {
        entity.MachineId = dto.MachineId;
        entity.ApplicationId = dto.ApplicationId;
        entity.AppStageId = dto.AppStageId;
        entity.ProcessorArchitectureId = dto.ProcessorArchitectureId;
        entity.DnsEndpointId = dto.DnsEndpointId;
        entity.RootPath = dto.RootPath;
        entity.PhysicalPath = dto.PhysicalPath;
        entity.Tags = dto.Tags;
        entity.IsActive = dto.IsActive;
        entity.ValidFromDate = dto.ValidFromDate;
        entity.ValidToDate = dto.ValidToDate;
    }
}
