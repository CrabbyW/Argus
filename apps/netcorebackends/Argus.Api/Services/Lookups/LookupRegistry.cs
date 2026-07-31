using Argus.Api.Database.Entities;
using Argus.Api.WebApiPoco.Common;

namespace Argus.Api.Services.Lookups;

/// <summary>
/// One entry per lookup kind. This is the only place that knows a kind exists — adding a tenth
/// is one entry here plus one enum value, and the <c>required</c> members mean the compiler will
/// not let the entry be half-written.
/// </summary>
public static class LookupRegistry
{
    private static readonly IReadOnlyDictionary<LookupKind, ILookupDescriptor> ByKind =
        new ILookupDescriptor[]
        {
            new LookupDescriptor<Machine>
            {
                Kind = LookupKind.Machines,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name, Description = x.Description },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => { e.Name = dto.Name; e.Description = dto.Description; },
                InUseBy = id => i => i.MachineId == id
            },

            new LookupDescriptor<AppName>
            {
                Kind = LookupKind.AppNames,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name, Description = x.Description },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => { e.Name = dto.Name; e.Description = dto.Description; },
                InUseBy = id => i => i.AppNameId == id
            },

            new LookupDescriptor<AppStageName>
            {
                Kind = LookupKind.AppStageNames,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name, SortOrder = x.SortOrder },
                OrderBy = q => q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name),
                Apply = (e, dto) => { e.Name = dto.Name; e.SortOrder = dto.SortOrder; },
                InUseBy = id => i => i.AppStageNameId == id
            },

            new LookupDescriptor<ProcessorArchitecture>
            {
                Kind = LookupKind.ProcessorArchitectures,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => e.Name = dto.Name,
                InUseBy = id => i => i.ProcessorArchitectureId == id
            },

            new LookupDescriptor<DnsEndpoint>
            {
                Kind = LookupKind.DnsEndpoints,
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
                InUseBy = id => i => i.DnsEndpointId == id
            },

            new LookupDescriptor<RootPath>
            {
                Kind = LookupKind.RootPaths,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => e.Name = dto.Name,
                InUseBy = id => i => i.RootPathId == id
            },

            new LookupDescriptor<PhysicalPath>
            {
                Kind = LookupKind.PhysicalPaths,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => e.Name = dto.Name,
                InUseBy = id => i => i.PhysicalPathId == id
            },

            new LookupDescriptor<Tag>
            {
                Kind = LookupKind.Tags,
                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name, Description = x.Description },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = (e, dto) => { e.Name = dto.Name; e.Description = dto.Description; },
                InUseBy = id => i => i.InstallationTags.Any(link => link.TagId == id)
            },

            new LookupDescriptor<AppRepository>
            {
                Kind = LookupKind.AppRepositories,

                // Read-only on purpose. RepositoryType and the installation links have nowhere to
                // live in LookupUpsertDto, so an ordinary read-modify-PUT through here would reset
                // the type to Unknown and drop every link. Reading is fine — a dropdown of
                // repositories is the same query as any other kind.
                IsReadOnly = true,

                Projection = x => new LookupItemDto { Id = x.Id, Name = x.Name, Description = x.Description },
                OrderBy = q => q.OrderBy(x => x.Name),
                Apply = static (_, _) => throw new NotSupportedException(
                    "AppRepositories are written through IAppRepositoryService."),
                InUseBy = id => i => i.InstallationRepositories.Any(link => link.AppRepositoryId == id)
            }
        }.ToDictionary(d => d.Kind);

    public static ILookupDescriptor Get(LookupKind kind) =>
        ByKind.TryGetValue(kind, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(
                  nameof(kind), kind, "No descriptor is registered for this lookup kind.");

    public static IReadOnlyCollection<ILookupDescriptor> All => (IReadOnlyCollection<ILookupDescriptor>)ByKind.Values;
}
