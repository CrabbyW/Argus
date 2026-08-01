using System.Linq.Expressions;
using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Services.Lookups;

/// <summary>
/// "Is this lookup row still referenced?" — the check that stands between a delete and an
/// installation left showing a blank name.
/// </summary>
public sealed class UsageProbe
{
    /// <summary>
    /// Plural noun for the error message ("installations", "repositories"). The message names what
    /// actually blocks the delete, which is the only thing the caller can act on.
    /// </summary>
    public required string UsedBy { get; init; }

    public required Func<ArgusDbContext, int, Task<bool>> IsInUseAsync { get; init; }
}

/// <summary>
/// Factories for <see cref="UsageProbe"/>, one per table that can hold a reference to a lookup.
///
/// Most kinds are referenced by <see cref="ApplicationInstallation"/>, and rooting the query there
/// is load-bearing rather than incidental: it is what makes the entity's <c>IsEnabled</c> query
/// filter apply, so a decommissioned installation does not block deleting a lookup it once used.
/// Each factory takes <c>Func&lt;int, Expression&gt;</c> rather than a stored expression so the id
/// arrives as a closure constant EF can parameterise.
/// </summary>
public static class Usage
{
    public static UsageProbe FromInstallations(
        Func<int, Expression<Func<ApplicationInstallation, bool>>> predicate) => new()
        {
            UsedBy = "installations",
            IsInUseAsync = (db, id) => db.ApplicationInstallations.AnyAsync(predicate(id))
        };

    /// <summary>
    /// For lookups an installation never names directly. <c>RepositoryTypes</c> is the case: it
    /// hangs off <see cref="AppRepository"/>, and asking through the installation instead would
    /// let a type used only by repositories that are not currently deployed be deleted, orphaning
    /// the foreign key.
    /// </summary>
    public static UsageProbe FromRepositories(
        Func<int, Expression<Func<AppRepository, bool>>> predicate) => new()
        {
            UsedBy = "repositories",
            IsInUseAsync = (db, id) => db.AppRepositories.AnyAsync(predicate(id))
        };
}
