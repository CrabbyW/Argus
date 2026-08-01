using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class RepositoryTypeConfiguration : IEntityTypeConfiguration<RepositoryType>
{
    public void Configure(EntityTypeBuilder<RepositoryType> builder)
    {
        builder.ToTable("RepositoryTypes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasColumnName("RepositoryTypeName").IsRequired().HasMaxLength(64);
        builder.Property(x => x.Description).HasMaxLength(512);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name)
               .IsUnique()
               .HasFilter("[IsEnabled] = 1")
               .HasDatabaseName("UX_RepositoryTypes_RepositoryTypeName");

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
