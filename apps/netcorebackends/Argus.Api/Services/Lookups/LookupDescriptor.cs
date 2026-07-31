using System.Linq.Expressions;
using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.WebApiPoco.Common;

namespace Argus.Api.Services.Lookups;

/// <summary>
/// The non-generic face of a descriptor, so <see cref="LookupRegistry"/> can hold all nine in one
/// dictionary and hand out handlers without reflection.
/// </summary>
public interface ILookupDescriptor
{
    LookupKind Kind { get; }

    /// <summary>True when the kind can only be read through the generic lookup layer.</summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// The entity this kind maps to. Exposed so callers that need the kind's configuration — the
    /// per-kind name length above all — can read it off the EF model instead of keeping a second
    /// copy of the mapping that would drift from this registry.
    /// </summary>
    Type EntityType { get; }

    ILookupHandler CreateHandler(ArgusDbContext db);
}

/// <summary>
/// Everything the generic handler needs to know about one lookup kind, written once per kind in
/// concrete-typed lambdas. Concrete types matter: a projection written against
/// <see cref="ILookupEntity"/> puts interface members into the expression tree, which EF resolves
/// only by name-matching. Here every lambda sees the real entity, so it always translates to SQL.
/// </summary>
public sealed class LookupDescriptor<TEntity> : ILookupDescriptor
    where TEntity : class, ILookupEntity, new()
{
    public required LookupKind Kind { get; init; }

    /// <summary>
    /// Projection to the wire DTO. Must be pure member reads — it is both translated to SQL for
    /// queries and compiled and run in memory to shape a row that was just saved, and those two
    /// have to agree.
    /// </summary>
    public required Expression<Func<TEntity, LookupItemDto>> Projection { get; init; }

    /// <summary>
    /// Default ordering. A delegate over <c>IQueryable</c> rather than
    /// <c>Expression&lt;Func&lt;TEntity, object&gt;&gt;</c>: the latter boxes value types like
    /// SortOrder and stops translating.
    /// </summary>
    public required Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> OrderBy { get; init; }

    /// <summary>
    /// Copies the writable payload onto a new or existing row. This is where the columns only some
    /// kinds have (SortOrder, IsLoadBalancer, Description) are handled.
    /// </summary>
    public required Action<TEntity, LookupUpsertDto> Apply { get; init; }

    /// <summary>
    /// "Is this row still referenced?", always expressed from <see cref="ApplicationInstallation"/>
    /// rather than from the lookup's own navigation. Rooting the query there is load-bearing: it
    /// is what makes the IsEnabled query filter apply, so a decommissioned installation does not
    /// block deleting a lookup. A <c>Func</c> returning the expression, not a stored expression, so
    /// the id arrives as a closure constant EF can parameterise.
    /// </summary>
    public required Func<int, Expression<Func<ApplicationInstallation, bool>>> InUseBy { get; init; }

    public bool IsReadOnly { get; init; }

    public Type EntityType => typeof(TEntity);

    private Func<TEntity, LookupItemDto>? compiledProjection;

    /// <summary>
    /// The same projection compiled for in-memory use, so a create/update can shape its result
    /// without a second round trip. Compiled once, on first use.
    /// </summary>
    public Func<TEntity, LookupItemDto> ProjectInMemory =>
        compiledProjection ??= Projection.Compile();

    public ILookupHandler CreateHandler(ArgusDbContext db) => new LookupHandler<TEntity>(db, this);
}
