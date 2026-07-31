using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class PhysicalPathConfiguration : IEntityTypeConfiguration<PhysicalPath>
{
    public void Configure(EntityTypeBuilder<PhysicalPath> builder)
    {
        builder.ToTable("PhysicalPaths");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasColumnName("Path").IsRequired().HasMaxLength(512);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name)
               .IsUnique()
               .HasFilter("[IsEnabled] = 1")
               .HasDatabaseName("UX_PhysicalPaths_Path");

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
