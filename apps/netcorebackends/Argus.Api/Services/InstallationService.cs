using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.Mappers;
using Argus.Api.WebApiPoco.Common;
using Argus.Api.WebApiPoco.Installations;
using log4net;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Services;

public class InstallationService : IInstallationService
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(InstallationService));

    private readonly ArgusDbContext db;

    public InstallationService(ArgusDbContext db)
    {
        this.db = db;
    }

    public async Task<DataViewOutput<InstallationListItemDto>> GetInstallationsAsync(InstallationFilterDto filter)
    {
        var query = db.Installations
            .AsNoTracking()
            .Include(x => x.Machine)
            .Include(x => x.Application)
            .Include(x => x.AppStage)
            .Include(x => x.ProcessorArchitecture)
            .Include(x => x.DnsEndpoint)
            .AsQueryable();

        if (filter.IncludeDisabled)
        {
            // Historical questions must be able to see decommissioned rows, which are
            // soft-deleted. IgnoreQueryFilters drops the IsEnabled filter on Installations.
            query = query.IgnoreQueryFilters();
        }

        if (filter.MachineId.HasValue)
        {
            query = query.Where(x => x.MachineId == filter.MachineId.Value);
        }

        if (filter.ApplicationId.HasValue)
        {
            query = query.Where(x => x.ApplicationId == filter.ApplicationId.Value);
        }

        if (filter.AppStageId.HasValue)
        {
            query = query.Where(x => x.AppStageId == filter.AppStageId.Value);
        }

        if (filter.ProcessorArchitectureId.HasValue)
        {
            query = query.Where(x => x.ProcessorArchitectureId == filter.ProcessorArchitectureId.Value);
        }

        if (filter.DnsEndpointId.HasValue)
        {
            query = query.Where(x => x.DnsEndpointId == filter.DnsEndpointId.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.IsActive.Value);
        }

        // Overlap, not containment: an installation that started before the window and is
        // still running belongs in the answer to "what was installed during this period?".
        // A null ValidToDate means "still installed", so it never fails the upper bound.
        if (filter.ValidFrom.HasValue)
        {
            var from = filter.ValidFrom.Value;
            query = query.Where(x => x.ValidToDate == null || x.ValidToDate >= from);
        }

        if (filter.ValidTo.HasValue)
        {
            var to = filter.ValidTo.Value;
            query = query.Where(x => x.ValidFromDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(x =>
                EF.Functions.Like(x.Machine.MachineName, $"%{term}%") ||
                EF.Functions.Like(x.Application.AppName, $"%{term}%") ||
                EF.Functions.Like(x.RootPath, $"%{term}%") ||
                (x.PhysicalPath != null && EF.Functions.Like(x.PhysicalPath, $"%{term}%")) ||
                (x.Tags != null && EF.Functions.Like(x.Tags, $"%{term}%")) ||
                (x.DnsEndpoint != null && EF.Functions.Like(x.DnsEndpoint.DnsName, $"%{term}%")));
        }

        var totalCount = await query.CountAsync();

        query = ApplySort(query, filter);

        var items = await query
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync();

        return new DataViewOutput<InstallationListItemDto>
        {
            Items = items.Select(InstallationMapper.ToListItemDto).ToList(),
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        };
    }

    /// <summary>
    /// Whitelisted sort columns only — the client sends a column name, never raw SQL.
    /// </summary>
    private static IQueryable<Installation> ApplySort(IQueryable<Installation> query, InstallationFilterDto filter)
    {
        var desc = filter.IsDescending;

        return (filter.SortBy?.ToLowerInvariant()) switch
        {
            "appname" => desc
                ? query.OrderByDescending(x => x.Application.AppName)
                : query.OrderBy(x => x.Application.AppName),
            "appstagename" => desc
                ? query.OrderByDescending(x => x.AppStage.SortOrder)
                : query.OrderBy(x => x.AppStage.SortOrder),
            "rootpath" => desc
                ? query.OrderByDescending(x => x.RootPath)
                : query.OrderBy(x => x.RootPath),
            "dnsname" => desc
                ? query.OrderByDescending(x => x.DnsEndpoint!.DnsName)
                : query.OrderBy(x => x.DnsEndpoint!.DnsName),
            "isactive" => desc
                ? query.OrderByDescending(x => x.IsActive)
                : query.OrderBy(x => x.IsActive),
            "validfromdate" => desc
                ? query.OrderByDescending(x => x.ValidFromDate)
                : query.OrderBy(x => x.ValidFromDate),
            _ => desc
                ? query.OrderByDescending(x => x.Machine.MachineName).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.Machine.MachineName).ThenBy(x => x.Id)
        };
    }

    public async Task<InstallationDetailDto?> GetInstallationByIdAsync(int id)
    {
        var entity = await LoadDetailQuery(tracking: false)
            .FirstOrDefaultAsync(x => x.Id == id);

        return entity is null ? null : InstallationMapper.ToDetailDto(entity);
    }

    public async Task<InstallationDetailDto> CreateInstallationAsync(InstallationUpsertDto dto)
    {
        await ValidateReferencesAsync(dto);
        await ValidateUniqueDeploymentAsync(dto, excludeId: null);

        var entity = new Installation
        {
            CreatedUtc = DateTime.UtcNow
        };

        InstallationMapper.ApplyUpsert(entity, dto);

        if (entity.ValidFromDate == default)
        {
            entity.ValidFromDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        db.Installations.Add(entity);
        await db.SaveChangesAsync();

        logger.Info($"Created installation {entity.Id} (machine={entity.MachineId}, app={entity.ApplicationId}).");

        // Reload with navigations so the response carries resolved names.
        return (await GetInstallationByIdAsync(entity.Id))!;
    }

    public async Task<InstallationDetailDto?> UpdateInstallationAsync(int id, InstallationUpsertDto dto)
    {
        var entity = await db.Installations.FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
        {
            return null;
        }

        await ValidateReferencesAsync(dto);
        await ValidateUniqueDeploymentAsync(dto, excludeId: id);

        InstallationMapper.ApplyUpsert(entity, dto);
        entity.ModifiedUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.Info($"Updated installation {id}.");

        return await GetInstallationByIdAsync(id);
    }

    public async Task<bool> DeleteInstallationAsync(int id)
    {
        var entity = await db.Installations.FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
        {
            return false;
        }

        // Soft delete — the row stays for audit, the query filter hides it.
        entity.IsEnabled = false;
        entity.ModifiedUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.Info($"Soft-deleted installation {id}.");
        return true;
    }

    private IQueryable<Installation> LoadDetailQuery(bool tracking)
    {
        var query = db.Installations.AsQueryable();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query
            .Include(x => x.Machine)
            .Include(x => x.Application).ThenInclude(a => a.AppRepositories)
            .Include(x => x.AppStage)
            .Include(x => x.ProcessorArchitecture)
            .Include(x => x.DnsEndpoint);
    }

    /// <summary>
    /// Every lookup Id must point at an existing, enabled row. Throws
    /// <see cref="ArgumentException"/> so the controller can turn it into a 400.
    /// </summary>
    private async Task ValidateReferencesAsync(InstallationUpsertDto dto)
    {
        if (!await db.Machines.AnyAsync(x => x.Id == dto.MachineId))
        {
            throw new ArgumentException($"Machine {dto.MachineId} does not exist.");
        }

        if (!await db.Applications.AnyAsync(x => x.Id == dto.ApplicationId))
        {
            throw new ArgumentException($"Application {dto.ApplicationId} does not exist.");
        }

        if (!await db.AppStages.AnyAsync(x => x.Id == dto.AppStageId))
        {
            throw new ArgumentException($"AppStage {dto.AppStageId} does not exist.");
        }

        if (!await db.ProcessorArchitectures.AnyAsync(x => x.Id == dto.ProcessorArchitectureId))
        {
            throw new ArgumentException($"ProcessorArchitecture {dto.ProcessorArchitectureId} does not exist.");
        }

        if (dto.DnsEndpointId.HasValue &&
            !await db.DnsEndpoints.AnyAsync(x => x.Id == dto.DnsEndpointId.Value))
        {
            throw new ArgumentException($"DnsEndpoint {dto.DnsEndpointId.Value} does not exist.");
        }

        if (dto.ValidToDate.HasValue && dto.ValidToDate.Value < dto.ValidFromDate)
        {
            throw new ArgumentException("ValidToDate cannot be earlier than ValidFromDate.");
        }
    }

    /// <summary>
    /// Mirrors the unique index so the user gets a readable message instead of a
    /// raw SQL constraint violation. Both sides deliberately ignore decommissioned rows:
    /// the index is filtered on <c>IsEnabled = 1</c>, and the query filter on
    /// <see cref="ArgusDbContext.Installations"/> hides the same rows here. Installing
    /// something again after it was retired is a new period, not a duplicate.
    /// </summary>
    private async Task ValidateUniqueDeploymentAsync(InstallationUpsertDto dto, int? excludeId)
    {
        var clash = await db.Installations.AnyAsync(x =>
            x.MachineId == dto.MachineId &&
            x.ApplicationId == dto.ApplicationId &&
            x.AppStageId == dto.AppStageId &&
            x.RootPath == dto.RootPath &&
            (excludeId == null || x.Id != excludeId.Value));

        if (clash)
        {
            throw new ArgumentException(
                "This application and stage is already installed at that path on that machine.");
        }
    }
}
