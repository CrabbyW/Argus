using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.WebApiPoco.Common;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Services.Lookups;

/// <summary>The per-kind operations, hidden behind a non-generic face for the service façade.</summary>
public interface ILookupHandler
{
    Task<IReadOnlyList<LookupItemDto>> GetAllAsync();

    Task<LookupItemDto?> GetByIdAsync(int id);

    Task<LookupItemDto> CreateAsync(LookupUpsertDto dto);

    Task<LookupItemDto?> UpdateAsync(int id, LookupUpsertDto dto);

    Task<bool> DeleteAsync(int id);
}

/// <summary>
/// The CRUD every lookup shares, written once. Everything that differs per kind arrives in the
/// descriptor, so this class never switches on <see cref="LookupKind"/>.
/// </summary>
internal sealed class LookupHandler<TEntity> : ILookupHandler
    where TEntity : class, ILookupEntity, new()
{
    private readonly ArgusDbContext db;
    private readonly LookupDescriptor<TEntity> descriptor;

    public LookupHandler(ArgusDbContext db, LookupDescriptor<TEntity> descriptor)
    {
        this.db = db;
        this.descriptor = descriptor;
    }

    /// <summary>Carries the global IsEnabled query filter, exactly as <c>db.Machines</c> would.</summary>
    private IQueryable<TEntity> Rows => db.Set<TEntity>();

    public async Task<IReadOnlyList<LookupItemDto>> GetAllAsync() =>
        await descriptor.OrderBy(Rows.AsNoTracking())
                        .Select(descriptor.Projection)
                        .ToListAsync();

    public async Task<LookupItemDto?> GetByIdAsync(int id) =>
        await Rows.AsNoTracking()
                  .Where(x => x.Id == id)
                  .Select(descriptor.Projection)
                  .FirstOrDefaultAsync();

    public async Task<LookupItemDto> CreateAsync(LookupUpsertDto dto)
    {
        EnsureWritable();

        var name = dto.Name.Trim();
        EnsureNameFits(name);
        await EnsureNameIsFreeAsync(name, excludeId: null);

        var entity = new TEntity();
        descriptor.Apply(entity, dto);
        entity.Name = name;

        db.Set<TEntity>().Add(entity);
        await db.SaveChangesAsync();

        // SaveChanges filled in the Id, so the row can be shaped here instead of re-queried.
        return descriptor.ProjectInMemory(entity);
    }

    public async Task<LookupItemDto?> UpdateAsync(int id, LookupUpsertDto dto)
    {
        EnsureWritable();

        var name = dto.Name.Trim();
        EnsureNameFits(name);
        await EnsureNameIsFreeAsync(name, excludeId: id);

        var entity = await Rows.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return null;
        }

        // Renaming here is the single-source-of-truth edit: every installation referencing this
        // Id shows the new name immediately.
        descriptor.Apply(entity, dto);
        entity.Name = name;

        await db.SaveChangesAsync();

        return descriptor.ProjectInMemory(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        EnsureWritable();

        var entity = await Rows.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            // Missing before referenced: a caller deleting an id that is not there deserves a 404,
            // not the "still in use" error.
            return false;
        }

        await EnsureNotInUseAsync(id);

        entity.IsEnabled = false;
        await db.SaveChangesAsync();
        return true;
    }

    private void EnsureWritable()
    {
        if (descriptor.IsReadOnly)
        {
            throw new NotSupportedException(
                $"{descriptor.Kind} cannot be written through the lookup API.");
        }
    }

    /// <summary>
    /// The column width, read from the EF model rather than copied into a table here. The point of
    /// this check is to mirror the configuration; a hand-kept copy is exactly what drifts, and the
    /// drift only shows up as a raw SqlException on save.
    /// </summary>
    private int MaxNameLength() =>
        db.Model.FindEntityType(typeof(TEntity))!
          .FindProperty(nameof(ILookupEntity.Name))!
          .GetMaxLength()
        ?? throw new InvalidOperationException(
               $"{typeof(TEntity).Name}.Name has no HasMaxLength, so its length cannot be validated.");

    private void EnsureNameFits(string name)
    {
        var max = MaxNameLength();

        if (name.Length > max)
        {
            throw new ArgumentException($"{descriptor.Kind} names are limited to {max} characters.");
        }
    }

    private async Task EnsureNameIsFreeAsync(string name, int? excludeId)
    {
        // Chained rather than `(excludeId == null || x.Id != excludeId)` in one predicate: that
        // form emits a parameterised OR the server cannot answer from the unique index.
        var query = Rows.Where(x => x.Name == name);

        if (excludeId is int id)
        {
            query = query.Where(x => x.Id != id);
        }

        if (await query.AnyAsync())
        {
            throw new ArgumentException($"'{name}' already exists.");
        }
    }

    /// <summary>
    /// A lookup that live installations still point at cannot be removed — hiding it would leave
    /// those installations showing a blank name.
    /// </summary>
    private async Task EnsureNotInUseAsync(int id)
    {
        if (await db.ApplicationInstallations.AnyAsync(descriptor.InUseBy(id)))
        {
            throw new ArgumentException(
                "This item is still used by one or more installations and cannot be deleted.");
        }
    }
}
