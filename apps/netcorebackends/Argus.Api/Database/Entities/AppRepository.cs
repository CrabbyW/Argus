namespace Argus.Api.Database.Entities;

/// <summary>
/// A source-control location an installation's code comes from
/// (svn://..., git://..., bitbucket://...).
///
/// The url is stored once and linked to every installation built from it through
/// <see cref="InstallationRepository"/> — several installations of the same application normally
/// share one repository, so hanging a copy off each of them would duplicate the same fact.
///
/// It implements <see cref="ILookupEntity"/> so it can be read through the generic lookup layer
/// (a dropdown of repositories is the same query as any other kind), but its descriptor is marked
/// read-only: <c>RepositoryType</c> and the installation links have nowhere to live in
/// <c>LookupUpsertDto</c>, so writes go through <c>IAppRepositoryService</c>.
/// </summary>
public class AppRepository : ILookupEntity
{
    public int Id { get; set; }

    /// <summary>Stored in the <c>RepositoryUrl</c> column.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional: null means the source-control system was never recorded. This replaced the
    /// enum's <c>Unknown</c> member — an absent value is a null foreign key here, the same
    /// convention <c>ApplicationInstallation.DnsEndpointId</c> already uses.
    /// </summary>
    public int? RepositoryTypeId { get; set; }

    public RepositoryType? RepositoryType { get; set; }

    public string? Description { get; set; }

    /// <summary>Soft-delete flag: 0 = hidden, 1 = active.</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<InstallationRepository> InstallationRepositories { get; set; } =
        new List<InstallationRepository>();
}
