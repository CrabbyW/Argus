using Argus.Api.Database;
using Argus.Api.Database.Entities;

namespace Argus.Api.Services.Lookups;

/// <summary>
/// What the EF model itself can answer about a lookup kind, so nothing has to be written down
/// twice.
/// </summary>
public static class LookupModel
{
    /// <summary>
    /// The name column's width, read from the model rather than copied into a table somewhere.
    /// Both the write check and the metadata endpoint go through here: a hand-kept copy is exactly
    /// what drifts from the configuration, and the drift only shows up as a raw SqlException on
    /// save — or, once the UI has its own copy, as a form that happily accepts a name the server
    /// will reject.
    /// </summary>
    public static int MaxNameLength(ArgusDbContext db, Type entityType) =>
        db.Model.FindEntityType(entityType)!
          .FindProperty(nameof(ILookupEntity.Name))!
          .GetMaxLength()
        ?? throw new InvalidOperationException(
               $"{entityType.Name}.Name has no HasMaxLength, so its length cannot be validated.");
}
