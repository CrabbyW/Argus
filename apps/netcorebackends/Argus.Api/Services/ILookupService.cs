using Argus.Api.WebApiPoco.Common;

namespace Argus.Api.Services;

/// <summary>
/// The lookup tables share one shape (Id + Name + IsEnabled), so they share one service.
/// New kinds are appended, never inserted — the numeric values are stable.
///
/// These are the tables that must be filled before an installation can be recorded: an
/// installation holds nothing but references into them.
/// </summary>
public enum LookupKind
{
    Machines,
    AppNames,
    AppStageNames,
    ProcessorArchitectures,
    DnsEndpoints,
    RootPaths,
    PhysicalPaths,
    Tags,

    /// <summary>
    /// Readable through this service, but not writable: <see cref="LookupUpsertDto"/> has nowhere
    /// to put the repository type or the installation links, so a round-trip through here would
    /// erase both. Writes go through <see cref="IAppRepositoryService"/>.
    /// </summary>
    AppRepositories,

    /// <summary>
    /// Appended rather than slotted in next to <see cref="AppRepositories"/>, which reads better:
    /// the values are stable by the rule above, and inserting would renumber AppRepositories.
    /// </summary>
    RepositoryTypes
}

public interface ILookupService
{
    /// <summary>
    /// Describes every kind: what to call it, which optional fields it has, how long a name may
    /// be. The lookup screen builds itself from this, so a new kind needs no frontend change.
    /// </summary>
    IReadOnlyList<LookupMetadataDto> GetMetadata();

    Task<IReadOnlyList<LookupItemDto>> GetAllAsync(LookupKind kind);

    Task<LookupItemDto?> GetByIdAsync(LookupKind kind, int id);

    /// <summary>Throws <see cref="NotSupportedException"/> for a read-only kind.</summary>
    Task<LookupItemDto> CreateAsync(LookupKind kind, LookupUpsertDto dto);

    /// <summary>Throws <see cref="NotSupportedException"/> for a read-only kind.</summary>
    Task<LookupItemDto?> UpdateAsync(LookupKind kind, int id, LookupUpsertDto dto);

    /// <summary>
    /// Soft delete. Throws <see cref="ArgumentException"/> if the row is still referenced —
    /// by a live installation for most kinds, by a repository for RepositoryTypes —
    /// and <see cref="NotSupportedException"/> for a read-only kind.
    /// </summary>
    Task<bool> DeleteAsync(LookupKind kind, int id);
}
