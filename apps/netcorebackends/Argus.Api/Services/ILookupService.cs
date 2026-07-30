using Argus.Api.WebApiPoco.Common;

namespace Argus.Api.Services;

/// <summary>
/// The five lookup tables share one shape (Id + Name), so they share one service.
/// </summary>
public enum LookupKind
{
    Machines,
    Applications,
    AppStages,
    ProcessorArchitectures,
    DnsEndpoints
}

public interface ILookupService
{
    Task<IReadOnlyList<LookupItemDto>> GetAllAsync(LookupKind kind);

    Task<LookupItemDto?> GetByIdAsync(LookupKind kind, int id);

    Task<LookupItemDto> CreateAsync(LookupKind kind, LookupUpsertDto dto);

    Task<LookupItemDto?> UpdateAsync(LookupKind kind, int id, LookupUpsertDto dto);

    /// <summary>Soft delete. Throws <see cref="ArgumentException"/> if still referenced.</summary>
    Task<bool> DeleteAsync(LookupKind kind, int id);
}
