namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: a public DNS name an installation is reachable at
/// (e.g. https://helpdesk.demo.example). Deliberately its own table because a single DNS name may be
/// a load balancer fronting several machines — it is shared, not owned by one installation.
/// </summary>
public class DnsEndpoint : ILookupEntity
{
    public int Id { get; set; }

    /// <summary>Stored in the <c>DnsName</c> column.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>True when this name points at a load balancer rather than a single machine.</summary>
    public bool IsLoadBalancer { get; set; }

    public string? Description { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<ApplicationInstallation> Installations { get; set; } =
        new List<ApplicationInstallation>();
}
