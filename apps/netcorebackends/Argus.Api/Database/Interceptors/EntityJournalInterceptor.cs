using Argus.Api.Database.Entities;
using Argus.Api.Services;
using log4net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Argus.Api.Database.Interceptors;

/// <summary>
/// Fills <see cref="EntityJournalEntry"/> from the change tracker.
///
/// It lives here rather than in the services because the change tracker is the only place that
/// knows what a value <em>was</em>: <c>InstallationService.UpdateInstallationAsync</c> loads the
/// installation tracked, so <c>OriginalValues</c> against <c>CurrentValues</c> is a real diff and
/// nothing has to be re-read to produce one. The second reason is coverage —
/// <c>AppRepositoryService</c> edits <c>InstallationRepositories</c> rows from the repository end,
/// and a journal written by hand in <c>InstallationService</c> would quietly miss every link made
/// from the Repositories screen.
///
/// What it does not do: it never journals lookups, users or repositories themselves. This table is
/// the history of an installation, and a row without an installation has nowhere to hang.
/// </summary>
public class EntityJournalInterceptor : SaveChangesInterceptor
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(EntityJournalInterceptor));

    /// <summary>
    /// Columns whose change is not news. <c>ModifiedUtc</c> moves on every single edit and would
    /// double the size of the table saying nothing; <c>CreatedUtc</c> never legitimately changes.
    /// <c>IsEnabled</c> is handled separately, as the soft delete it is.
    /// </summary>
    private static readonly HashSet<string> IgnoredProperties = new()
    {
        nameof(ApplicationInstallation.CreatedUtc),
        nameof(ApplicationInstallation.ModifiedUtc),
        nameof(ApplicationInstallation.IsEnabled)
    };

    private readonly ICurrentUserAccessor currentUser;

    /// <summary>
    /// Installations inserted by the save currently in flight. Their journal rows cannot be
    /// written with the rest: the row's foreign key needs an installation Id, and that Id does not
    /// exist until the insert has run. They are picked up again in <see cref="SavedChangesAsync"/>.
    /// </summary>
    private readonly List<PendingCreate> pendingCreates = new();

    public EntityJournalInterceptor(ICurrentUserAccessor currentUser)
    {
        this.currentUser = currentUser;
    }

    private sealed record PendingCreate(ApplicationInstallation Entity, Guid ChangeSetId, DateTime WhenUtc);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        WriteCreates(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await WriteCreatesAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => pendingCreates.Clear();

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        pendingCreates.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Turns everything pending in the change tracker into journal rows and adds them to the same
    /// save. Same transaction as the change itself, so a rolled-back edit cannot leave a journal
    /// row claiming it happened.
    /// </summary>
    private void Capture(DbContext? context)
    {
        if (context is not ArgusDbContext db || db.JournalingSuppressed)
        {
            return;
        }

        var changeSetId = Guid.NewGuid();
        var whenUtc = DateTime.UtcNow;
        var changedBy = currentUser.Username;
        var resolver = new ValueResolver(db);
        var rows = new List<EntityJournalEntry>();

        // Materialised before anything is added: adding to the context mutates the entry list.
        var installations = db.ChangeTracker.Entries<ApplicationInstallation>().ToList();
        var tagLinks = db.ChangeTracker.Entries<InstallationTag>().ToList();
        var repositoryLinks = db.ChangeTracker.Entries<InstallationRepository>().ToList();

        foreach (var entry in installations)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    pendingCreates.Add(new PendingCreate(entry.Entity, changeSetId, whenUtc));
                    break;

                case EntityState.Modified:
                    rows.AddRange(CaptureModified(entry, resolver, changeSetId, whenUtc, changedBy));
                    break;
            }
        }

        foreach (var entry in tagLinks)
        {
            var row = CaptureLink(
                entry,
                nameof(InstallationTag),
                "Tag",
                link => resolver.Lookup<Tag>(link.TagId),
                changeSetId,
                whenUtc,
                changedBy);

            if (row is not null)
            {
                rows.Add(row);
            }
        }

        foreach (var entry in repositoryLinks)
        {
            var row = CaptureLink(
                entry,
                nameof(InstallationRepository),
                "Repository",
                link => resolver.Lookup<AppRepository>(link.AppRepositoryId),
                changeSetId,
                whenUtc,
                changedBy);

            if (row is not null)
            {
                rows.Add(row);
            }
        }

        if (rows.Count > 0)
        {
            db.EntityJournal.AddRange(rows);
        }
    }

    private static IEnumerable<EntityJournalEntry> CaptureModified(
        EntityEntry<ApplicationInstallation> entry,
        ValueResolver resolver,
        Guid changeSetId,
        DateTime whenUtc,
        string changedBy)
    {
        var enabled = entry.Property(x => x.IsEnabled);

        // A soft delete is not "the IsEnabled column went false" to anyone reading this later.
        if (enabled.IsModified && !Equals(enabled.OriginalValue, enabled.CurrentValue))
        {
            yield return new EntityJournalEntry
            {
                ChangeSetId = changeSetId,
                InstallationId = entry.Entity.Id,
                EntityName = nameof(ApplicationInstallation),
                Action = enabled.CurrentValue ? JournalActions.Restored : JournalActions.Deleted,
                ChangedBy = changedBy,
                ChangedUtc = whenUtc
            };
        }

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;

            if (!property.IsModified
                || IgnoredProperties.Contains(name)
                || Equals(property.OriginalValue, property.CurrentValue))
            {
                continue;
            }

            var field = JournalFields.Describe(name);

            if (field is null)
            {
                // An unmapped column is a code change nobody extended the map for. Recording it
                // by its property name is still better than dropping the change silently.
                field = new JournalField(name, null);
            }

            yield return new EntityJournalEntry
            {
                ChangeSetId = changeSetId,
                InstallationId = entry.Entity.Id,
                EntityName = nameof(ApplicationInstallation),
                Action = JournalActions.Updated,
                Field = field.Display,
                OldValue = resolver.Describe(field, property.OriginalValue),
                NewValue = resolver.Describe(field, property.CurrentValue),
                OldValueId = field.IsReference ? property.OriginalValue as int? : null,
                NewValueId = field.IsReference ? property.CurrentValue as int? : null,
                ChangedBy = changedBy,
                ChangedUtc = whenUtc
            };
        }
    }

    /// <summary>
    /// A link row added or removed. Returns null when there is nothing to record — including the
    /// links of an installation being created, whose Id is still 0: the <c>Created</c> row already
    /// says the installation came into existence with them, and a list of every tag it was born
    /// with is noise rather than history.
    /// </summary>
    private static EntityJournalEntry? CaptureLink<TLink>(
        EntityEntry<TLink> entry,
        string entityName,
        string field,
        Func<TLink, string?> describe,
        Guid changeSetId,
        DateTime whenUtc,
        string changedBy)
        where TLink : class
    {
        if (entry.State is not (EntityState.Added or EntityState.Deleted))
        {
            return null;
        }

        var isAdded = entry.State == EntityState.Added;

        // Added rows carry the current values, deleted rows only the original ones.
        var installationId = (int)(isAdded
            ? entry.CurrentValues[nameof(InstallationTag.InstallationId)]!
            : entry.OriginalValues[nameof(InstallationTag.InstallationId)]!);

        if (installationId == 0)
        {
            return null;
        }

        var value = describe(entry.Entity);

        return new EntityJournalEntry
        {
            ChangeSetId = changeSetId,
            InstallationId = installationId,
            EntityName = entityName,
            Action = isAdded ? JournalActions.LinkAdded : JournalActions.LinkRemoved,
            Field = field,
            OldValue = isAdded ? null : value,
            NewValue = isAdded ? value : null,
            ChangedBy = changedBy,
            ChangedUtc = whenUtc
        };
    }

    private void WriteCreates(DbContext? context)
    {
        if (context is not ArgusDbContext db)
        {
            return;
        }

        var rows = TakeCreateRows(db);

        if (rows.Count > 0)
        {
            SaveJournalRows(db, rows, () => db.SaveChanges());
        }
    }

    private async Task WriteCreatesAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is not ArgusDbContext db)
        {
            return;
        }

        var rows = TakeCreateRows(db);

        if (rows.Count > 0)
        {
            await SaveJournalRowsAsync(db, rows, () => db.SaveChangesAsync(cancellationToken));
        }
    }

    /// <summary>
    /// The second save, the one that carries the <c>Created</c> rows. Suppression is on around it
    /// for two reasons at once: these rows are not themselves journaled, and without it the save
    /// would re-enter this interceptor.
    ///
    /// A failure here is logged, not thrown. The installation is already stored and correct by
    /// this point, and an edit that succeeded must not be reported to the user as an error.
    /// </summary>
    private static void SaveJournalRows(ArgusDbContext db, List<EntityJournalEntry> rows, Action save)
    {
        db.JournalingSuppressed = true;

        try
        {
            db.EntityJournal.AddRange(rows);
            save();
        }
        catch (Exception ex)
        {
            logger.Error("Could not write the Created journal rows for a saved installation.", ex);
        }
        finally
        {
            db.JournalingSuppressed = false;
        }
    }

    private static async Task SaveJournalRowsAsync(
        ArgusDbContext db,
        List<EntityJournalEntry> rows,
        Func<Task> save)
    {
        db.JournalingSuppressed = true;

        try
        {
            db.EntityJournal.AddRange(rows);
            await save();
        }
        catch (Exception ex)
        {
            logger.Error("Could not write the Created journal rows for a saved installation.", ex);
        }
        finally
        {
            db.JournalingSuppressed = false;
        }
    }

    /// <summary>
    /// Builds the rows for the creates of the save that has just completed and forgets them, so a
    /// context that saves twice cannot write the same row again.
    /// </summary>
    private List<EntityJournalEntry> TakeCreateRows(ArgusDbContext db)
    {
        if (db.JournalingSuppressed || pendingCreates.Count == 0)
        {
            return new List<EntityJournalEntry>();
        }

        var rows = pendingCreates
            .Where(create => create.Entity.Id != 0)
            .Select(create => new EntityJournalEntry
            {
                ChangeSetId = create.ChangeSetId,
                InstallationId = create.Entity.Id,
                EntityName = nameof(ApplicationInstallation),
                Action = JournalActions.Created,
                ChangedBy = currentUser.Username,
                ChangedUtc = create.WhenUtc
            })
            .ToList();

        pendingCreates.Clear();

        return rows;
    }
}
