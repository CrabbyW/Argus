using Argus.Api.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class InstallationRepositoryConfiguration : IEntityTypeConfiguration<InstallationRepository>
{
    public void Configure(EntityTypeBuilder<InstallationRepository> builder)
    {
        builder.ToTable("InstallationRepositories");
        builder.HasKey(x => new { x.InstallationId, x.AppRepositoryId });

        builder.HasOne(x => x.Installation)
               .WithMany(i => i.InstallationRepositories)
               .HasForeignKey(x => x.InstallationId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AppRepository)
               .WithMany(r => r.InstallationRepositories)
               .HasForeignKey(x => x.AppRepositoryId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AppRepositoryId);

        // No HasQueryFilter here on purpose — see the note on InstallationRepository itself.
    }
}
