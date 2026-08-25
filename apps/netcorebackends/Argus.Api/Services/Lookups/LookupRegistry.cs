using Argus.Api.Database.Entities;
using Argus.Api.WebApiPoco.Common;

namespace Argus.Api.Services.Lookups;

/// <summary>
/// One entry per lookup kind. This is the only place that knows a kind exists — adding an
/// eleventh is one entry here plus one enum value, and the <c>required</c> members mean the
/// compiler will not let the entry be half-written.
///
/// Label, Singular and the Has* flags are served by <c>GET /api/lookups</c>, so the screen builds
/// its tabs and form fields from this list. A kind added here shows up in the UI on its own; the
/// frontend has no second copy of it to forget.
/// </summary>
public static class LookupRegistry
{
    private static readonly IReadOnlyDictionary<LookupKind, ILookupDescriptor> ByKind =
        new ILookupDescriptor[]
        {
            new LookupDescriptor<Machine>
            {
                Kind = LookupKind.Machines,
                Label = "Machines",
                Singular = "machine",
                HasDescription = true,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name, Description = x.Description },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => { e.Name = dto.Name; e.Description = dto.Description; },
                NormalizeName = LookupFormats.NormalizeMachine,
                ValidateName = LookupFormats.ValidateMachine,
                NameHint = "A host name, stored upper case — e.g. BOREAS01.",
                Usage = Usage.FromInstallations(id => i => i.MachineId == id)
            },

            new LookupDescriptor<AppName>
            {
                Kind = LookupKind.AppNames,
                Label = "Applications",
                Singular = "application",
                HasDescription = true,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name, Description = x.Description },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => { e.Name = dto.Name; e.Description = dto.Description; },
                Usage = Usage.FromInstallations(id => i => i.AppNameId == id)
            },

            new LookupDescriptor<AppStageName>
            {
                Kind = LookupKind.AppStageNames,
                Label = "Stages",
                Singular = "stage",
                HasSortOrder = true,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name, SortOrder = x.SortOrder },
                OrderBy = q => q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name),
                Apply = (e, dto) => { e.Name = dto.Name; e.SortOrder = dto.SortOrder; },
                Usage = Usage.FromInstallations(id => i => i.AppStageNameId == id)
            },

            new LookupDescriptor<ProcessorArchitecture>
            {
                Kind = LookupKind.ProcessorArchitectures,
                Label = "Architectures",
                Singular = "architecture",
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => e.Name = dto.Name,
                Usage = Usage.FromInstallations(id => i => i.ProcessorArchitectureId == id)
            },

            new LookupDescriptor<DnsEndpoint>
            {
                Kind = LookupKind.DnsEndpoints,
                Label = "DNS endpoints",
                Singular = "DNS endpoint",
                HasDescription = true,
                HasLoadBalancer = true,
                Projection = x => new LookupItemDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    IsLoadBalancer = x.IsLoadBalancer
                },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) =>
                {
                    e.Name = dto.Name;
                    e.Description = dto.Description;
                    e.IsLoadBalancer = dto.IsLoadBalancer;
                },
                // A host name, never a URL: a pasted address is reduced to its host, and what
                // cannot be read as a host name at all is refused.
                NormalizeName = DnsName.Normalize,
                ValidateName = LookupFormats.ValidateDnsName,
                NameHint = "A host name — e.g. helpdesk.demo.example. A pasted URL is reduced to its host.",
                Usage = Usage.FromInstallations(id => i => i.DnsEndpointId == id)
            },

            new LookupDescriptor<RootPath>
            {
                Kind = LookupKind.RootPaths,
                Label = "Root paths",
                Singular = "root path",
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => e.Name = dto.Name,
                NormalizeName = LookupFormats.NormalizeRootPath,
                ValidateName = LookupFormats.ValidateRootPath,
                NameHint = "A url path starting with / — e.g. /helpdesk.rc0. The site root is /.",
                Usage = Usage.FromInstallations(id => i => i.RootPathId == id)
            },

            new LookupDescriptor<PhysicalPath>
            {
                Kind = LookupKind.PhysicalPaths,
                Label = "Physical paths",
                Singular = "physical path",
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => e.Name = dto.Name,
                NormalizeName = LookupFormats.NormalizePhysicalPath,
                ValidateName = LookupFormats.ValidatePhysicalPath,
                NameHint = @"An absolute path — e.g. c:\inetpub\helpdesk or \\server\share.",
                Usage = Usage.FromInstallations(id => i => i.PhysicalPathId == id)
            },

            new LookupDescriptor<Tag>
            {
                Kind = LookupKind.Tags,
                Label = "Tags",
                Singular = "tag",
                HasDescription = true,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name, Description = x.Description },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => { e.Name = dto.Name; e.Description = dto.Description; },
                NormalizeName = LookupFormats.NormalizeTag,
                ValidateName = LookupFormats.ValidateTag,
                NameHint = "Lower case, words joined by hyphens — e.g. incoming-postal-web.",
                Usage = Usage.FromInstallations(id => i => i.InstallationTags.Any(link => link.TagId == id))
            },

            new LookupDescriptor<RepositoryType>
            {
                Kind = LookupKind.RepositoryTypes,
                Label = "Repository types",
                Singular = "repository type",
                HasDescription = true,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name, Description = x.Description },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => { e.Name = dto.Name; e.Description = dto.Description; },

                // The one kind an installation does not reference directly — see Usage.
                Usage = Usage.FromRepositories(id => r => r.RepositoryTypeId == id)
            },

            new LookupDescriptor<AppRepository>
            {
                Kind = LookupKind.AppRepositories,
                Label = "Repositories",
                Singular = "repository",
                HasDescription = true,

                // Read-only on purpose. The repository type and the installation links have nowhere
                // to live in LookupUpsertDto, so an ordinary read-modify-PUT through here would
                // clear the type and drop every link. Reading is fine — a dropdown of repositories
                // is the same query as any other kind.
                IsReadOnly = true,

                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name, Description = x.Description },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = static (_, _) => throw new NotSupportedException(
                    "AppRepositories are written through IAppRepositoryService."),
                Usage = Usage.FromInstallations(id => i => i.InstallationRepositories.Any(link => link.AppRepositoryId == id))
            }
        }.ToDictionary(d => d.Kind);

    public static ILookupDescriptor Get(LookupKind kind) =>
        ByKind.TryGetValue(kind, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(
                  nameof(kind), kind, "No descriptor is registered for this lookup kind.");

    public static IReadOnlyCollection<ILookupDescriptor> All => (IReadOnlyCollection<ILookupDescriptor>)ByKind.Values;
}
