using Argus.Api.WebApiPoco.Common;

namespace Argus.Api.WebApiPoco.Installations;

/// <summary>One row of an installation's history, as the detail screen shows it.</summary>
public class JournalEntryDto
{
    public int Id { get; set; }

    /// <summary>Rows sharing this were written by one save — one edit by one person.</summary>
    public Guid ChangeSetId { get; set; }

    public DateTime ChangedUtc { get; set; }

    public string ChangedBy { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? Field { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public int? OldValueId { get; set; }

    public int? NewValueId { get; set; }
}

/// <summary>Criteria for reading one installation's history.</summary>
public class JournalReadRequestDto : ReadRequestDto
{
    /// <summary>
    /// How many of the newest entries to return. Clamped by the service — the history of a row
    /// edited daily for years is not something a drawer should ever ask for whole.
    /// </summary>
    public int MaxEntries { get; set; } = 200;
}
