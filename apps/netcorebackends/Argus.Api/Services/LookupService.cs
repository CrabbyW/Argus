using Argus.Api.Database;
using Argus.Api.Services.Lookups;
using Argus.Api.WebApiPoco.Common;
using log4net;

namespace Argus.Api.Services;

/// <summary>
/// A façade over <see cref="LookupRegistry"/>. All ten kinds share one implementation
/// (<see cref="LookupHandler{TEntity}"/>); what differs per kind lives in its descriptor.
/// </summary>
public class LookupService : ILookupService
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(LookupService));

    private readonly ArgusDbContext db;

    public LookupService(ArgusDbContext db)
    {
        this.db = db;
    }

    private ILookupHandler HandlerFor(LookupKind kind) => LookupRegistry.Get(kind).CreateHandler(db);

    public IReadOnlyList<LookupMetadataDto> GetMetadata() =>
        LookupRegistry.All
            .Select(descriptor => new LookupMetadataDto
            {
                // Lower-case so the client can drop it straight into a url. The route itself is
                // case-insensitive, but a kind that arrives ready to use is one fewer thing for a
                // caller to get subtly wrong.
                Kind = descriptor.Kind.ToString().ToLowerInvariant(),
                Label = descriptor.Label,
                Singular = descriptor.Singular,
                IsReadOnly = descriptor.IsReadOnly,
                MaxNameLength = LookupModel.MaxNameLength(db, descriptor.EntityType),
                HasDescription = descriptor.HasDescription,
                HasSortOrder = descriptor.HasSortOrder,
                HasLoadBalancer = descriptor.HasLoadBalancer
            })
            .ToList();

    public Task<IReadOnlyList<LookupItemDto>> GetAllAsync(LookupKind kind) =>
        HandlerFor(kind).GetAllAsync();

    public Task<LookupItemDto?> GetByIdAsync(LookupKind kind, int id) =>
        HandlerFor(kind).GetByIdAsync(id);

    public async Task<LookupItemDto> CreateAsync(LookupKind kind, LookupUpsertDto dto)
    {
        var created = await HandlerFor(kind).CreateAsync(dto);
        logger.Info($"Created {kind} lookup '{created.Name}'.");
        return created;
    }

    public async Task<LookupItemDto?> UpdateAsync(LookupKind kind, int id, LookupUpsertDto dto)
    {
        var updated = await HandlerFor(kind).UpdateAsync(id, dto);

        if (updated is not null)
        {
            logger.Info($"Updated {kind} lookup {id} to '{updated.Name}'.");
        }

        return updated;
    }

    public async Task<bool> DeleteAsync(LookupKind kind, int id)
    {
        var deleted = await HandlerFor(kind).DeleteAsync(id);

        if (deleted)
        {
            logger.Info($"Soft-deleted {kind} lookup {id}.");
        }

        return deleted;
    }
}
