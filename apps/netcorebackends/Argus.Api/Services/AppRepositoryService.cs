using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.Mappers;
using Argus.Api.WebApiPoco.Installations;
using log4net;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Services;

public class AppRepositoryService : IAppRepositoryService
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(AppRepositoryService));

    private readonly ArgusDbContext db;

    public AppRepositoryService(ArgusDbContext db)
    {
        this.db = db;
    }

    public async Task<IReadOnlyList<AppRepositoryDto>> GetAllAsync(int? applicationId)
    {
        var query = db.AppRepositories.AsNoTracking();

        if (applicationId is not null)
        {
            query = query.Where(x => x.ApplicationId == applicationId);
        }

        var entities = await query
            .OrderBy(x => x.ApplicationId).ThenBy(x => x.RepositoryUrl)
            .ToListAsync();

        return entities.Select(InstallationMapper.ToAppRepositoryDto).ToList();
    }

    public async Task<AppRepositoryDto?> GetByIdAsync(int id)
    {
        var entity = await db.AppRepositories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

        return entity is null ? null : InstallationMapper.ToAppRepositoryDto(entity);
    }

    public async Task<AppRepositoryDto> CreateAsync(AppRepositoryUpsertDto dto)
    {
        var url = dto.RepositoryUrl.Trim();

        await EnsureApplicationExistsAsync(dto.ApplicationId);
        await EnsureUrlIsFreeAsync(dto.ApplicationId, url, excludeId: null);

        var entity = new AppRepository
        {
            ApplicationId = dto.ApplicationId,
            RepositoryUrl = url,
            RepositoryType = dto.RepositoryType,
            Description = dto.Description
        };

        db.AppRepositories.Add(entity);
        await db.SaveChangesAsync();
        logger.Info($"Created repository {entity.Id} ('{url}') for application {dto.ApplicationId}.");

        return InstallationMapper.ToAppRepositoryDto(entity);
    }

    public async Task<AppRepositoryDto?> UpdateAsync(int id, AppRepositoryUpsertDto dto)
    {
        var entity = await db.AppRepositories.FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
        {
            return null;
        }

        var url = dto.RepositoryUrl.Trim();

        await EnsureApplicationExistsAsync(dto.ApplicationId);
        await EnsureUrlIsFreeAsync(dto.ApplicationId, url, excludeId: id);

        entity.ApplicationId = dto.ApplicationId;
        entity.RepositoryUrl = url;
        entity.RepositoryType = dto.RepositoryType;
        entity.Description = dto.Description;

        await db.SaveChangesAsync();
        logger.Info($"Updated repository {id} to '{url}'.");

        return InstallationMapper.ToAppRepositoryDto(entity);
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

    private async Task EnsureApplicationExistsAsync(int applicationId)
    {
        if (!await db.Applications.AnyAsync(x => x.Id == applicationId))
        {
            throw new ArgumentException($"Application {applicationId} does not exist.");
        }
    }

    /// <summary>
    /// The same URL twice under one application is a duplicate record, not two repositories.
    /// Across different applications it is legitimate — a shared library, for instance.
    /// </summary>
    private async Task EnsureUrlIsFreeAsync(int applicationId, string url, int? excludeId)
    {
        var taken = await db.AppRepositories.AnyAsync(x =>
            x.ApplicationId == applicationId
            && x.RepositoryUrl == url
            && (excludeId == null || x.Id != excludeId));

        if (taken)
        {
            throw new ArgumentException($"'{url}' is already registered for this application.");
        }
    }
}
