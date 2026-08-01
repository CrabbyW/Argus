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
        var query = db.ApplicationInstallations
            .AsNoTracking()
            .Include(x => x.Machine)
            .Include(x => x.AppName)
            .Include(x => x.AppStageName)
            .Include(x => x.ProcessorArchitecture)
            .Include(x => x.DnsEndpoint)
            .Include(x => x.RootPath)
            .Include(x => x.PhysicalPath)
            .Include(x => x.InstallationTags).ThenInclude(link => link.Tag)
            .AsQueryable();

        if (filter.IncludeDisabled)
        {
            // Historical questions must be able to see decommissioned rows, which are
            // soft-deleted. IgnoreQueryFilters drops the IsEnabled filter.
            query = query.IgnoreQueryFilters();
        }

        if (filter.MachineId.HasValue)
        {
            query = query.Where(x => x.MachineId == filter.MachineId.Value);
        }

        if (filter.AppNameId.HasValue)
        {
            query = query.Where(x => x.AppNameId == filter.AppNameId.Value);
        }

        if (filter.AppStageNameId.HasValue)
        {
            query = query.Where(x => x.AppStageNameId == filter.AppStageNameId.Value);
        }

        if (filter.ProcessorArchitectureId.HasValue)
        {
            query = query.Where(x => x.ProcessorArchitectureId == filter.ProcessorArchitectureId.Value);
        }

        if (filter.DnsEndpointId.HasValue)
        {
            query = query.Where(x => x.DnsEndpointId == filter.DnsEndpointId.Value);
        }

        if (filter.RootPathId.HasValue)
        {
            query = query.Where(x => x.RootPathId == filter.RootPathId.Value);
        }

        if (filter.PhysicalPathId.HasValue)
        {
            query = query.Where(x => x.PhysicalPathId == filter.PhysicalPathId.Value);
        }

        // Any(), never a join: CountAsync below runs on this same query, and a join over the
        // link table would return one row per matching tag and inflate TotalCount.
        if (filter.TagId.HasValue)
        {
            query = query.Where(x => x.InstallationTags.Any(link => link.TagId == filter.TagId.Value));
        }

        if (filter.RepositoryId.HasValue)
        {
            query = query.Where(x =>
                x.InstallationRepositories.Any(link => link.AppRepositoryId == filter.RepositoryId.Value));
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
                EF.Functions.Like(x.Machine.Name, $"%{term}%") ||
                EF.Functions.Like(x.AppName.Name, $"%{term}%") ||
                EF.Functions.Like(x.RootPath.Name, $"%{term}%") ||
                (x.PhysicalPath != null && EF.Functions.Like(x.PhysicalPath.Name, $"%{term}%")) ||
                x.InstallationTags.Any(link => EF.Functions.Like(link.Tag.Name, $"%{term}%")) ||
                (x.DnsEndpoint != null && EF.Functions.Like(x.DnsEndpoint.Name, $"%{term}%")));
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
    private static IQueryable<ApplicationInstallation> ApplySort(
        IQueryable<ApplicationInstallation> query, InstallationFilterDto filter)
    {
        var desc = filter.IsDescending;

        return (filter.SortBy?.ToLowerInvariant()) switch
        {
            "appname" => desc
                ? query.OrderByDescending(x => x.AppName.Name)
                : query.OrderBy(x => x.AppName.Name),
            "appstagename" => desc
                ? query.OrderByDescending(x => x.AppStageName.SortOrder)
                : query.OrderBy(x => x.AppStageName.SortOrder),
            "rootpath" => desc
                ? query.OrderByDescending(x => x.RootPath.Name)
                : query.OrderBy(x => x.RootPath.Name),
            "physicalpath" => desc
                ? query.OrderByDescending(x => x.PhysicalPath!.Name)
                : query.OrderBy(x => x.PhysicalPath!.Name),
            "dnsname" => desc
                ? query.OrderByDescending(x => x.DnsEndpoint!.Name)
                : query.OrderBy(x => x.DnsEndpoint!.Name),
            "processorarchitecture" => desc
                ? query.OrderByDescending(x => x.ProcessorArchitecture.Name)
                : query.OrderBy(x => x.ProcessorArchitecture.Name),
            "isactive" => desc
                ? query.OrderByDescending(x => x.IsActive)
                : query.OrderBy(x => x.IsActive),
            "validfromdate" => desc
                ? query.OrderByDescending(x => x.ValidFromDate)
                : query.OrderBy(x => x.ValidFromDate),
            _ => desc
                ? query.OrderByDescending(x => x.Machine.Name).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.Machine.Name).ThenBy(x => x.Id)
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

        var entity = new ApplicationInstallation
        {
            CreatedUtc = DateTime.UtcNow
        };

        InstallationMapper.ApplyUpsert(entity, dto);

        if (entity.ValidFromDate == default)
        {
            entity.ValidFromDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        db.ApplicationInstallations.Add(entity);
        SyncLinks(entity, dto);
        await db.SaveChangesAsync();

        logger.Info($"Created installation {entity.Id} (machine={entity.MachineId}, app={entity.AppNameId}).");

        // Reload with navigations so the response carries resolved names.
        return (await GetInstallationByIdAsync(entity.Id))!;
    }

    public async Task<InstallationDetailDto?> UpdateInstallationAsync(int id, InstallationUpsertDto dto)
    {
        // The link collections have to be loaded, otherwise the diff below cannot see what is
        // already there and would try to insert links that exist.
        var entity = await db.ApplicationInstallations
            .Include(x => x.InstallationTags)
            .Include(x => x.InstallationRepositories)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
        {
            return null;
        }

        await ValidateReferencesAsync(dto);
        await ValidateUniqueDeploymentAsync(dto, excludeId: id);

        InstallationMapper.ApplyUpsert(entity, dto);
        SyncLinks(entity, dto);
        entity.ModifiedUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.Info($"Updated installation {id}.");

        return await GetInstallationByIdAsync(id);
    }

    public async Task<bool> DeleteInstallationAsync(int id)
    {
        var entity = await db.ApplicationInstallations.FirstOrDefaultAsync(x => x.Id == id);

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

    /// <summary>
    /// Brings the tag and repository links in line with the payload: removes the links that are
    /// no longer wanted, adds the ones that are missing, and leaves the rest untouched.
    ///
    /// Lives here rather than in <see cref="InstallationMapper"/> because deleting link rows
    /// needs the DbContext. <c>Distinct()</c> is load-bearing — a payload listing the same tag
    /// twice would otherwise violate the composite primary key.
    /// </summary>
    private void SyncLinks(ApplicationInstallation entity, InstallationUpsertDto dto)
    {
        var wantedTags = dto.TagIds.Distinct().ToHashSet();

        db.InstallationTags.RemoveRange(
            entity.InstallationTags.Where(link => !wantedTags.Contains(link.TagId)));

        db.InstallationTags.AddRange(
            wantedTags
                .Where(tagId => entity.InstallationTags.All(link => link.TagId != tagId))
                .Select(tagId => new InstallationTag { Installation = entity, TagId = tagId }));

        var wantedRepositories = dto.RepositoryIds.Distinct().ToHashSet();

        db.InstallationRepositories.RemoveRange(
            entity.InstallationRepositories.Where(link => !wantedRepositories.Contains(link.AppRepositoryId)));

        db.InstallationRepositories.AddRange(
            wantedRepositories
                .Where(repoId => entity.InstallationRepositories.All(link => link.AppRepositoryId != repoId))
                .Select(repoId => new InstallationRepository { Installation = entity, AppRepositoryId = repoId }));
    }

    private IQueryable<ApplicationInstallation> LoadDetailQuery(bool tracking)
    {
        var query = db.ApplicationInstallations.AsQueryable();

        if (!tracking)
        {
            // Identity resolution, not plain AsNoTracking: the include below walks
            // installation -> repository -> installations, and EF rejects a cycle in a plain
            // no-tracking query. With one identity map per query the cycle is fine.
            query = query.AsNoTrackingWithIdentityResolution();
        }

        return query
            .Include(x => x.Machine)
            .Include(x => x.AppName)
            .Include(x => x.AppStageName)
            .Include(x => x.ProcessorArchitecture)
            .Include(x => x.DnsEndpoint)
            .Include(x => x.RootPath)
            .Include(x => x.PhysicalPath)
            .Include(x => x.InstallationTags).ThenInclude(link => link.Tag)
            .Include(x => x.InstallationRepositories)
                .ThenInclude(link => link.AppRepository)
                    // Without this the repository's own link collection is filled by relationship
                    // fixup alone, so it holds just the installation being read and the detail
                    // payload reports a truncated InstallationIds. Anything that sent such a
                    // repository back in a PUT would unlink it from its sibling installations,
                    // because AppRepositoryService.UpdateAsync treats InstallationIds as the
                    // complete target state.
                    .ThenInclude(repo => repo.InstallationRepositories)

            // Second branch off the same repository, for the type's display name.
            .Include(x => x.InstallationRepositories)
                .ThenInclude(link => link.AppRepository)
                    .ThenInclude(repo => repo.RepositoryType);
    }

    /// <summary>
    /// Every lookup Id must point at an existing, enabled row — this is where "the lookups are
    /// filled first" is enforced. Throws <see cref="ArgumentException"/> so the controller can
    /// turn it into a 400.
    /// </summary>
    private async Task ValidateReferencesAsync(InstallationUpsertDto dto)
    {
        if (!await db.Machines.AnyAsync(x => x.Id == dto.MachineId))
        {
            throw new ArgumentException($"Machine {dto.MachineId} does not exist.");
        }

        if (!await db.AppNames.AnyAsync(x => x.Id == dto.AppNameId))
        {
            throw new ArgumentException($"AppName {dto.AppNameId} does not exist.");
        }

        if (!await db.AppStageNames.AnyAsync(x => x.Id == dto.AppStageNameId))
        {
            throw new ArgumentException($"AppStageName {dto.AppStageNameId} does not exist.");
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

        if (!await db.RootPaths.AnyAsync(x => x.Id == dto.RootPathId))
        {
            throw new ArgumentException($"RootPath {dto.RootPathId} does not exist.");
        }

        if (dto.PhysicalPathId.HasValue &&
            !await db.PhysicalPaths.AnyAsync(x => x.Id == dto.PhysicalPathId.Value))
        {
            throw new ArgumentException($"PhysicalPath {dto.PhysicalPathId.Value} does not exist.");
        }

        foreach (var tagId in dto.TagIds.Distinct())
        {
            if (!await db.Tags.AnyAsync(x => x.Id == tagId))
            {
                throw new ArgumentException($"Tag {tagId} does not exist.");
            }
        }

        foreach (var repositoryId in dto.RepositoryIds.Distinct())
        {
            if (!await db.AppRepositories.AnyAsync(x => x.Id == repositoryId))
            {
                throw new ArgumentException($"Repository {repositoryId} does not exist.");
            }
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
    /// <see cref="ArgusDbContext.ApplicationInstallations"/> hides the same rows here. Installing
    /// something again after it was retired is a new period, not a duplicate.
    /// </summary>
    private async Task ValidateUniqueDeploymentAsync(InstallationUpsertDto dto, int? excludeId)
    {
        var clash = await db.ApplicationInstallations.AnyAsync(x =>
            x.MachineId == dto.MachineId &&
            x.AppNameId == dto.AppNameId &&
            x.AppStageNameId == dto.AppStageNameId &&
            x.RootPathId == dto.RootPathId &&
            (excludeId == null || x.Id != excludeId.Value));

        if (clash)
        {
            throw new ArgumentException(
                "This application and stage is already installed at that path on that machine.");
        }
    }
}
