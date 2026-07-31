namespace Argus.Api.Database.Entities;

/// <summary>
/// The shape every lookup table shares: an id, a unique name, and a soft-delete flag.
///
/// The name property is called <c>Name</c> on every lookup even though the database column keeps
/// its own name (MachineName, StageName, Path, ...) via <c>HasColumnName</c>. That is what lets
/// <see cref="Services.Lookups.LookupHandler{TEntity}"/> be written once instead of once per kind.
///
/// Columns only some kinds have — Description, SortOrder, IsLoadBalancer — are deliberately absent.
/// Marker interfaces for them would put interface members into EF expression trees, which translate
/// only by name-matching; the per-kind descriptor has the concrete type in hand and reads them
/// directly instead.
/// </summary>
public interface ILookupEntity
{
    int Id { get; set; }

    string Name { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    bool IsEnabled { get; set; }
}
