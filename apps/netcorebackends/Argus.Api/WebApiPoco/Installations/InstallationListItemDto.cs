namespace Argus.Api.WebApiPoco.Installations;

/// <summary>Flattened row for the installations grid — names resolved from the lookups.</summary>
public class InstallationListItemDto
{
    public int Id { get; set; }

    public string MachineName { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    public string AppStageName { get; set; } = string.Empty;

    public string ProcessorArchitecture { get; set; } = string.Empty;

    public string? DnsName { get; set; }

    public string RootPath { get; set; } = string.Empty;

    /// <summary>Disk path on the machine — in the grid so it is answerable without opening a row.</summary>
    public string? PhysicalPath { get; set; }

    /// <summary>Resolved tag names, sorted — the grid renders one badge per entry.</summary>
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

    public bool IsActive { get; set; }

    public DateOnly ValidFromDate { get; set; }

    public DateOnly? ValidToDate { get; set; }
}
