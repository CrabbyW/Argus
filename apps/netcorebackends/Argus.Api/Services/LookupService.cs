using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.WebApiPoco.Common;
using log4net;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Services;

public class LookupService : ILookupService
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(LookupService));

    private readonly ArgusDbContext db;

    public LookupService(ArgusDbContext db)
    {
        this.db = db;
    }

    public async Task<IReadOnlyList<LookupItemDto>> GetAllAsync(LookupKind kind) => kind switch
    {
        LookupKind.Machines => await db.Machines.AsNoTracking()
            .OrderBy(x => x.MachineName)
            .Select(x => new LookupItemDto { Id = x.Id, Name = x.MachineName, Description = x.Description })
            .ToListAsync(),

        LookupKind.Applications => await db.Applications.AsNoTracking()
            .OrderBy(x => x.AppName)
            .Select(x => new LookupItemDto { Id = x.Id, Name = x.AppName, Description = x.Description })
            .ToListAsync(),

        LookupKind.AppStages => await db.AppStages.AsNoTracking()
            .OrderBy(x => x.SortOrder).ThenBy(x => x.StageName)
            .Select(x => new LookupItemDto { Id = x.Id, Name = x.StageName, SortOrder = x.SortOrder })
            .ToListAsync(),

        LookupKind.ProcessorArchitectures => await db.ProcessorArchitectures.AsNoTracking()
            .OrderBy(x => x.ArchitectureName)
            .Select(x => new LookupItemDto { Id = x.Id, Name = x.ArchitectureName })
            .ToListAsync(),

        LookupKind.DnsEndpoints => await db.DnsEndpoints.AsNoTracking()
            .OrderBy(x => x.DnsName)
            .Select(x => new LookupItemDto
            {
                Id = x.Id,
                Name = x.DnsName,
                Description = x.Description,
                IsLoadBalancer = x.IsLoadBalancer
            })
            .ToListAsync(),

        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public async Task<LookupItemDto?> GetByIdAsync(LookupKind kind, int id)
    {
        var all = await GetAllAsync(kind);
        return all.FirstOrDefault(x => x.Id == id);
    }

    public async Task<LookupItemDto> CreateAsync(LookupKind kind, LookupUpsertDto dto)
    {
        var name = dto.Name.Trim();
        await EnsureNameIsFreeAsync(kind, name, excludeId: null);

        switch (kind)
        {
            case LookupKind.Machines:
                db.Machines.Add(new Machine { MachineName = name, Description = dto.Description });
                break;
            case LookupKind.Applications:
                db.Applications.Add(new Application { AppName = name, Description = dto.Description });
                break;
            case LookupKind.AppStages:
                db.AppStages.Add(new AppStage { StageName = name, SortOrder = dto.SortOrder });
                break;
            case LookupKind.ProcessorArchitectures:
                db.ProcessorArchitectures.Add(new ProcessorArchitecture { ArchitectureName = name });
                break;
            case LookupKind.DnsEndpoints:
                db.DnsEndpoints.Add(new DnsEndpoint
                {
                    DnsName = name,
                    Description = dto.Description,
                    IsLoadBalancer = dto.IsLoadBalancer
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        await db.SaveChangesAsync();
        logger.Info($"Created {kind} lookup '{name}'.");

        var created = (await GetAllAsync(kind)).First(x => x.Name == name);
        return created;
    }

    public async Task<LookupItemDto?> UpdateAsync(LookupKind kind, int id, LookupUpsertDto dto)
    {
        var name = dto.Name.Trim();
        await EnsureNameIsFreeAsync(kind, name, excludeId: id);

        // Renaming here is the single-source-of-truth edit: every installation
        // referencing this Id shows the new name immediately.
        switch (kind)
        {
            case LookupKind.Machines:
            {
                var e = await db.Machines.FirstOrDefaultAsync(x => x.Id == id);
                if (e is null) return null;
                e.MachineName = name;
                e.Description = dto.Description;
                break;
            }
            case LookupKind.Applications:
            {
                var e = await db.Applications.FirstOrDefaultAsync(x => x.Id == id);
                if (e is null) return null;
                e.AppName = name;
                e.Description = dto.Description;
                break;
            }
            case LookupKind.AppStages:
            {
                var e = await db.AppStages.FirstOrDefaultAsync(x => x.Id == id);
                if (e is null) return null;
                e.StageName = name;
                e.SortOrder = dto.SortOrder;
                break;
            }
            case LookupKind.ProcessorArchitectures:
            {
                var e = await db.ProcessorArchitectures.FirstOrDefaultAsync(x => x.Id == id);
                if (e is null) return null;
                e.ArchitectureName = name;
                break;
            }
            case LookupKind.DnsEndpoints:
            {
                var e = await db.DnsEndpoints.FirstOrDefaultAsync(x => x.Id == id);
                if (e is null) return null;
                e.DnsName = name;
                e.Description = dto.Description;
                e.IsLoadBalancer = dto.IsLoadBalancer;
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        await db.SaveChangesAsync();
        logger.Info($"Updated {kind} lookup {id} to '{name}'.");

        return await GetByIdAsync(kind, id);
    }

    public async Task<bool> DeleteAsync(LookupKind kind, int id)
    {
        await EnsureNotInUseAsync(kind, id);

        switch (kind)
        {
            case LookupKind.Machines:
            {
                var e = await db.Machines.FirstOrDefaultAsync(x => x.Id == id);
                if (e is null) return false;
                e.IsEnabled = false;
                break;
            }
            case LookupKind.Applications:
            {
                var e = await db.Applications.FirstOrDefaultAsync(x => x.Id == id);
                if (e is null) return false;
                e.IsEnabled = false;
                break;
            }
            case LookupKind.AppStages:
            {
                var e = await db.AppStages.FirstOrDefaultAsync(x => x.Id == id);
                if (e is null) return false;
                e.IsEnabled = false;
                break;
            }
            case LookupKind.ProcessorArchitectures:
            {
                var e = await db.ProcessorArchitectures.FirstOrDefaultAsync(x => x.Id == id);
                if (e is null) return false;
                e.IsEnabled = false;
                break;
            }
            case LookupKind.DnsEndpoints:
            {
                var e = await db.DnsEndpoints.FirstOrDefaultAsync(x => x.Id == id);
                if (e is null) return false;
                e.IsEnabled = false;
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        await db.SaveChangesAsync();
        logger.Info($"Soft-deleted {kind} lookup {id}.");
        return true;
    }

    private async Task EnsureNameIsFreeAsync(LookupKind kind, string name, int? excludeId)
    {
        var taken = kind switch
        {
            LookupKind.Machines => await db.Machines
                .AnyAsync(x => x.MachineName == name && (excludeId == null || x.Id != excludeId)),
            LookupKind.Applications => await db.Applications
                .AnyAsync(x => x.AppName == name && (excludeId == null || x.Id != excludeId)),
            LookupKind.AppStages => await db.AppStages
                .AnyAsync(x => x.StageName == name && (excludeId == null || x.Id != excludeId)),
            LookupKind.ProcessorArchitectures => await db.ProcessorArchitectures
                .AnyAsync(x => x.ArchitectureName == name && (excludeId == null || x.Id != excludeId)),
            LookupKind.DnsEndpoints => await db.DnsEndpoints
                .AnyAsync(x => x.DnsName == name && (excludeId == null || x.Id != excludeId)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        if (taken)
        {
            throw new ArgumentException($"'{name}' already exists.");
        }
    }

    /// <summary>
    /// A lookup that installations still point at cannot be removed — hiding it would
    /// leave those installations showing a blank name.
    /// </summary>
    private async Task EnsureNotInUseAsync(LookupKind kind, int id)
    {
        var inUse = kind switch
        {
            LookupKind.Machines => await db.Installations.AnyAsync(x => x.MachineId == id),
            LookupKind.Applications => await db.Installations.AnyAsync(x => x.ApplicationId == id),
            LookupKind.AppStages => await db.Installations.AnyAsync(x => x.AppStageId == id),
            LookupKind.ProcessorArchitectures => await db.Installations.AnyAsync(x => x.ProcessorArchitectureId == id),
            LookupKind.DnsEndpoints => await db.Installations.AnyAsync(x => x.DnsEndpointId == id),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        if (inUse)
        {
            throw new ArgumentException(
                "This item is still used by one or more installations and cannot be deleted.");
        }
    }
}
