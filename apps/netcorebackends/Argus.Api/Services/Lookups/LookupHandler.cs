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

        var name = descriptor.NormalizeName(dto.Name);
        EnsureNameIsNotEmpty(name);
        EnsureNameFits(name);
        EnsureNameMatchesFormat(name);
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

        var name = descriptor.NormalizeName(dto.Name);
        EnsureNameIsNotEmpty(name);
        EnsureNameFits(name);
        EnsureNameMatchesFormat(name);
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
    /// Normalizing can empty a name the caller did fill in — "https://" reduces to nothing — so
    /// the check belongs here rather than only on the DTO.
    /// </summary>
    private void EnsureNameIsNotEmpty(string name)
    {
        if (name.Length == 0)
        {
            throw new ArgumentException($"A {descriptor.Singular} name is required.");
        }
    }

    private void EnsureNameFits(string name)
    {
        var max = LookupModel.MaxNameLength(db, typeof(TEntity));

        if (name.Length > max)
        {
            throw new ArgumentException($"{descriptor.Kind} names are limited to {max} characters.");
        }
    }

    /// <summary>
    /// Refuses a name that cannot be stored in the kind's format. Runs on the normalized value,
    /// so what reaches it is what would be written to the row — a pasted URL or a trailing
    /// backslash has already been dealt with, and what is left is a value that really is not a
    /// host name or not a path.
    /// </summary>
    private void EnsureNameMatchesFormat(string name)
    {
        if (descriptor.ValidateName(name) is string problem)
        {
            throw new ArgumentException(problem);
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
    /// A lookup that live rows still point at cannot be removed — hiding it would leave them
    /// showing a blank name.
    /// </summary>
    private async Task EnsureNotInUseAsync(int id)
    {
        if (await descriptor.Usage.IsInUseAsync(db, id))
        {
            throw new ArgumentException(
                $"This item is still used by one or more {descriptor.Usage.UsedBy} and cannot be deleted.");
        }
    }
}
