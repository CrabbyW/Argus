namespace Argus.Api.Database.Entities;

/// <summary>
/// Link row between an installation and the repository its code comes from.
///
/// Many-to-many rather than a foreign key on <see cref="AppRepository"/>: a repository url is one
/// fact, and several installations of the same application are built from it. A plain FK would
/// force one repository row per installation and store the same url over and over — the exact
/// duplication the rest of this model exists to remove.
///
/// Carries no <c>IsEnabled</c>, for the same reason as <see cref="InstallationTag"/>.
/// </summary>
public class InstallationRepository
{
    public int InstallationId { get; set; }
    public ApplicationInstallation Installation { get; set; } = null!;

    public int AppRepositoryId { get; set; }
    public AppRepository AppRepository { get; set; } = null!;
}
