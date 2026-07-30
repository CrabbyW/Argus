using Argus.Api.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class AppRepositoryConfiguration : IEntityTypeConfiguration<AppRepository>
{
    public void Configure(EntityTypeBuilder<AppRepository> builder)
    {
        builder.ToTable("AppRepositories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RepositoryUrl).IsRequired().HasMaxLength(512);
        builder.Property(x => x.RepositoryType).IsRequired().HasConversion<int>();
        builder.Property(x => x.Description).HasMaxLength(512);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasOne(x => x.Application)
               .WithMany(a => a.AppRepositories)
               .HasForeignKey(x => x.ApplicationId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ApplicationId, x.RepositoryUrl }).IsUnique();

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
