using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.Mappers;
using Argus.Api.WebApiPoco.Installations;
using log4net;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Services;

/// <summary>
/// The write path for repositories. They are readable through the generic lookup layer too, but
/// only there — RepositoryType and the installation links have nowhere to live in
/// <c>LookupUpsertDto</c>, so every change comes through here.
/// </summary>
public class AppRepositoryService : IAppRepositoryService
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(AppRepositoryService));

    private readonly ArgusDbContext db;

    public AppRepositoryService(ArgusDbContext db)
    {
        this.db = db;
    }

    public async Task<IReadOnlyList<AppRepositoryDto>> GetAllAsync(int? installationId, int? appNameId)
    {
        var query = db.AppRepositories
            .AsNoTracking()
            .Include(x => x.InstallationRepositories)
            .AsQueryable();

        if (installationId is not null)
        {
            query = query.Where(x => x.InstallationRepositories
                .Any(link => link.InstallationId == installationId));
        }

        // "Every repository used by any installation of this application" — the view the
        // Repositories screen had before repositories moved off the application itself.
        if (appNameId is not null)
        {
            query = query.Where(x => x.InstallationRepositories
                .Any(link => link.Installation.AppNameId == appNameId));
        }

        var entities = await query
            .OrderBy(x => x.Name)
            .ToListAsync();

        return entities.Select(InstallationMapper.ToAppRepositoryDto).ToList();
    }

    public async Task<AppRepositoryDto?> GetByIdAsync(int id)
    {
        var entity = await db.AppRepositories
            .AsNoTracking()
            .Include(x => x.InstallationRepositories)
            .FirstOrDefaultAsync(x => x.Id == id);

        return entity is null ? null : InstallationMapper.ToAppRepositoryDto(entity);
    }

    public async Task<AppRepositoryDto> CreateAsync(AppRepositoryUpsertDto dto)
    {
        var url = dto.RepositoryUrl.Trim();

        await EnsureUrlIsFreeAsync(url, excludeId: null);
        await EnsureInstallationsExistAsync(dto.InstallationIds);

        var entity = new AppRepository
        {
            Name = url,
            RepositoryType = dto.RepositoryType,
            Description = dto.Description
        };

        db.AppRepositories.Add(entity);
        SyncInstallationLinks(entity, dto);

        await db.SaveChangesAsync();
        logger.Info($"Created repository {entity.Id} ('{url}') linked to {dto.InstallationIds.Distinct().Count()} installation(s).");

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<AppRepositoryDto?> UpdateAsync(int id, AppRepositoryUpsertDto dto)
    {
        var entity = await db.AppRepositories
            .Include(x => x.InstallationRepositories)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
        {
            return null;
        }

        var url = dto.RepositoryUrl.Trim();

        await EnsureUrlIsFreeAsync(url, excludeId: id);
        await EnsureInstallationsExistAsync(dto.InstallationIds);

        entity.Name = url;
        entity.RepositoryType = dto.RepositoryType;
        entity.Description = dto.Description;

        SyncInstallationLinks(entity, dto);

        await db.SaveChangesAsync();
        logger.Info($"Updated repository {id} to '{url}'.");

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.AppRepositories.FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
        {
            return false;
        }

        entity.IsEnabled = false;
        await db.SaveChangesAsync();
        logger.Info($"Soft-deleted repository {id}.");

        return true;
    }

    /// <summary>
    /// The url identifies the repository. Now that a repository is shared across installations
    /// rather than owned by one application, the same url twice is always a duplicate record.
    /// Chained rather than one OR predicate, to match the unique index.
    /// </summary>
    private async Task EnsureUrlIsFreeAsync(string url, int? excludeId)
    {
        var query = db.AppRepositories.Where(x => x.Name == url);

        if (excludeId is int id)
        {
            query = query.Where(x => x.Id != id);
        }

        if (await query.AnyAsync())
        {
            throw new ArgumentException($"'{url}' is already registered.");
        }
    }

    private async Task EnsureInstallationsExistAsync(IEnumerable<int> installationIds)
    {
        foreach (var installationId in installationIds.Distinct())
        {
            if (!await db.ApplicationInstallations.AnyAsync(x => x.Id == installationId))
            {
                throw new ArgumentException($"Installation {installationId} does not exist.");
            }
        }
    }

    /// <summary>Same add/remove diff as <c>InstallationService.SyncLinks</c>, seen from the
    /// repository end of the relationship.</summary>
    private void SyncInstallationLinks(AppRepository entity, AppRepositoryUpsertDto dto)
    {
        var wanted = dto.InstallationIds.Distinct().ToHashSet();

        db.InstallationRepositories.RemoveRange(
            entity.InstallationRepositories.Where(link => !wanted.Contains(link.InstallationId)));

        db.InstallationRepositories.AddRange(
            wanted
                .Where(installationId =>
                    entity.InstallationRepositories.All(link => link.InstallationId != installationId))
                .Select(installationId => new InstallationRepository
                {
                    AppRepository = entity,
                    InstallationId = installationId
                }));
    }
}
