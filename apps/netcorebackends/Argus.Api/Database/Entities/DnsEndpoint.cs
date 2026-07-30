namespace Argus.Api.Database.Entities;

/// <summary>
/// Lookup: a public DNS name an installation is reachable at
/// (e.g. https://paha.ga.local). Deliberately its own table because a single DNS name may be
/// a load balancer fronting several machines — it is shared, not owned by one installation.
/// </summary>
public class DnsEndpoint
{
    public int Id { get; set; }

    public string DnsName { get; set; } = string.Empty;

    /// <summary>True when this name points at a load balancer rather than a single machine.</summary>
    public bool IsLoadBalancer { get; set; }

    public string? Description { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<Installation> Installations { get; set; } = new List<Installation>();
}
