using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class RootPathConfiguration : IEntityTypeConfiguration<RootPath>
{
    public void Configure(EntityTypeBuilder<RootPath> builder)
    {
        builder.ToTable("RootPaths");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasColumnName("Path").IsRequired().HasMaxLength(256);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name)
               .IsUnique()
               .HasFilter("[IsEnabled] = 1")
               .HasDatabaseName("UX_RootPaths_Path");

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
