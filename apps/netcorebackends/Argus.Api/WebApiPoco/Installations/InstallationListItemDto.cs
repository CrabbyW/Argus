namespace Argus.Api.WebApiPoco.Installations;

/// <summary>
/// Flattened row for the installations grid.
///
/// Carries both halves of every reference: the foreign key and the name it resolves to. The
/// roadplan's fact table is Ids only (<c>ApplicationInstalation [Id, MachineId, AppNameId, ...]</c>)
/// and the grid shows exactly that by default, so the Ids are not decoration — they are the
/// primary content. The names travel alongside for the hover text and the names view, and are
/// already loaded by the query either way.
/// </summary>
public class InstallationListItemDto
{
    public int Id { get; set; }

    public int MachineId { get; set; }

    public string MachineName { get; set; } = string.Empty;

    public int AppNameId { get; set; }

    public string AppName { get; set; } = string.Empty;

    public int AppStageNameId { get; set; }

    public string AppStageName { get; set; } = string.Empty;

    public int ProcessorArchitectureId { get; set; }

    public string ProcessorArchitecture { get; set; } = string.Empty;

    /// <summary>Null for a service or console app, which has no public address.</summary>
    public int? DnsEndpointId { get; set; }

    public string? DnsName { get; set; }

    public int RootPathId { get; set; }

    public string RootPath { get; set; } = string.Empty;

    /// <summary>Null where the installation records no disk path.</summary>
    public int? PhysicalPathId { get; set; }

    /// <summary>Disk path on the machine — in the grid so it is answerable without opening a row.</summary>
    public string? PhysicalPath { get; set; }

    /// <summary>Resolved tag names, sorted — the grid renders one badge per entry.</summary>
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

    public bool IsActive { get; set; }

    public DateOnly ValidFromDate { get; set; }

    public DateOnly? ValidToDate { get; set; }
}
