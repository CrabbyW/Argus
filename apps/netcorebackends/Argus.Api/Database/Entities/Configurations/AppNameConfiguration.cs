using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class AppNameConfiguration : IEntityTypeConfiguration<AppName>
{
    public void Configure(EntityTypeBuilder<AppName> builder)
    {
        builder.ToTable("AppNames");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasColumnName("AppName").IsRequired().HasMaxLength(128);
        builder.Property(x => x.Description).HasMaxLength(512);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name)
               .IsUnique()
               .HasFilter("[IsEnabled] = 1")
               .HasDatabaseName("UX_AppNames_AppName");

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
