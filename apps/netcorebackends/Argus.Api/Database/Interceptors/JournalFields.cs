using Argus.Api.Database.Entities;
using log4net;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Database.Interceptors;

/// <summary>The verbs the journal uses. Text in the database, constants here.</summary>
public static class JournalActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string Restored = "Restored";
    public const string LinkAdded = "LinkAdded";
    public const string LinkRemoved = "LinkRemoved";
}

/// <summary>
/// One journaled column: what to call it in the history, and — when it is a foreign key — which
/// lookup its value has to be read from.
/// </summary>
/// <param name="Display">The field as a person reads it: "Machine", not "MachineId".</param>
/// <param name="LookupType">The lookup entity behind the Id, or null for a plain column.</param>
public sealed record JournalField(string Display, Type? LookupType)
{
    public bool IsReference => LookupType is not null;
}

/// <summary>
/// The map from column to journal field.
///
/// Explicit rather than derived from the navigation properties: the labels here are the ones the
/// detail screen already uses, and a reader of the history should see the same words as a reader
/// of the installation.
/// </summary>
public static class JournalFields
{
    private static readonly Dictionary<string, JournalField> Fields = new()
    {
        [nameof(ApplicationInstallation.MachineId)] = new("Machine", typeof(Machine)),
        [nameof(ApplicationInstallation.AppNameId)] = new("Application", typeof(AppName)),
        [nameof(ApplicationInstallation.AppStageNameId)] = new("Stage", typeof(AppStageName)),
        [nameof(ApplicationInstallation.ProcessorArchitectureId)] =
            new("Architecture", typeof(ProcessorArchitecture)),
        [nameof(ApplicationInstallation.DnsEndpointId)] = new("DNS", typeof(DnsEndpoint)),
        [nameof(ApplicationInstallation.RootPathId)] = new("Root path", typeof(RootPath)),
        [nameof(ApplicationInstallation.PhysicalPathId)] = new("Physical path", typeof(PhysicalPath)),
        [nameof(ApplicationInstallation.IsActive)] = new("Active", null),
        [nameof(ApplicationInstallation.ValidFromDate)] = new("Valid from", null),
        [nameof(ApplicationInstallation.ValidToDate)] = new("Valid to", null)
    };

    public static JournalField? Describe(string propertyName) =>
        Fields.TryGetValue(propertyName, out var field) ? field : null;
}

/// <summary>
/// Turns a stored value into the text the journal keeps.
///
/// Foreign keys are resolved here, at write time, and that is the point: a machine renamed next
/// year must not change what the history says happened today. Results are cached for the length of
/// one save, because a single edit can touch the same lookup twice.
/// </summary>
public sealed class ValueResolver
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(ValueResolver));

    private readonly ArgusDbContext db;
    private readonly Dictionary<(Type, int), string?> cache = new();

    public ValueResolver(ArgusDbContext db)
    {
        this.db = db;
    }

    /// <summary>Null in, null out — an empty optional reference is recorded as no value, not as "0".</summary>
    public string? Describe(JournalField field, object? value) => value switch
    {
        null => null,
        int id when field.IsReference => Lookup(field.LookupType!, id),
        bool flag => flag ? "yes" : "no",
        DateOnly date => date.ToString("yyyy-MM-dd"),
        DateTime moment => moment.ToString("u"),
        _ => value.ToString()
    };

    public string? Lookup<TEntity>(int id) where TEntity : class, ILookupEntity => Lookup(typeof(TEntity), id);

    private string? Lookup(Type lookupType, int id)
    {
        if (cache.TryGetValue((lookupType, id), out var cached))
        {
            return cached;
        }

        var name = ReadName(lookupType, id);
        cache[(lookupType, id)] = name;

        return name;
    }

    private string? ReadName(Type lookupType, int id)
    {
        try
        {
            // The change tracker first: a lookup added in this very save has no row to read yet.
            var tracked = db.ChangeTracker.Entries()
                .FirstOrDefault(entry => entry.Entity.GetType() == lookupType
                                         && entry.Entity is ILookupEntity lookup
                                         && lookup.Id == id);

            if (tracked?.Entity is ILookupEntity trackedLookup)
            {
                return trackedLookup.Name;
            }

            // Find, not a Where: it is a key lookup, so the soft-delete query filter does not
            // apply to it. That is what is wanted here — a lookup disabled after the fact is
            // still the name that was on screen when the change was made.
            var entity = db.Find(lookupType, id);

            return entity is ILookupEntity found ? found.Name : Unresolved(id);
        }
        catch (Exception ex)
        {
            // Never let the history break the edit it is describing.
            logger.Warn($"Could not resolve the name of {lookupType.Name} {id} for the journal: {ex.Message}");
            return Unresolved(id);
        }
    }

    /// <summary>A row that is gone leaves its Id behind rather than a blank cell.</summary>
    private static string Unresolved(int id) => $"#{id}";
}
