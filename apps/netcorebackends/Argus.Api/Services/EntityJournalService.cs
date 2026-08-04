using Argus.Api.Database;
using Argus.Api.WebApiPoco.Installations;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Services;

public interface IEntityJournalService
{
    /// <summary>
    /// One installation's history, newest first. Null when there is no such installation — the
    /// caller turns that into a 404, which is not the same answer as "no changes recorded".
    /// </summary>
    Task<IReadOnlyList<JournalEntryDto>?> GetForInstallationAsync(int installationId, int maxEntries);
}

/// <summary>
/// Reads <c>EntityJournal</c>. There is no write side here on purpose: rows are appended by
/// <see cref="Database.Interceptors.EntityJournalInterceptor"/> and never edited or deleted, so a
/// service method that could do either would only be a way to falsify the record.
/// </summary>
public class EntityJournalService : IEntityJournalService
{
    /// <summary>Matches the paging cap the grids use; the drawer shows far fewer.</summary>
    private const int MaxEntriesCeiling = 500;

    private readonly ArgusDbContext db;

    public EntityJournalService(ArgusDbContext db)
    {
        this.db = db;
    }

    public async Task<IReadOnlyList<JournalEntryDto>?> GetForInstallationAsync(
        int installationId,
        int maxEntries)
    {
        // IgnoreQueryFilters: a soft-deleted installation still has a history, and "who deleted
        // this" is exactly the question someone opens it to answer.
        var exists = await db.ApplicationInstallations
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Id == installationId);

        if (!exists)
        {
            return null;
        }

        return await db.EntityJournal
            .AsNoTracking()
            .Where(x => x.InstallationId == installationId)
            // Id as the tie-breaker: the rows of one save share a timestamp to the tick, and
            // without it their order on screen would be whatever the database felt like.
            .OrderByDescending(x => x.ChangedUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(maxEntries, 1, MaxEntriesCeiling))
            .Select(x => new JournalEntryDto
            {
                Id = x.Id,
                ChangeSetId = x.ChangeSetId,
                ChangedUtc = x.ChangedUtc,
                ChangedBy = x.ChangedBy,
                Action = x.Action,
                EntityName = x.EntityName,
                Field = x.Field,
                OldValue = x.OldValue,
                NewValue = x.NewValue,
                OldValueId = x.OldValueId,
                NewValueId = x.NewValueId
            })
            .ToListAsync();
    }
}
