using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class AppStageNameConfiguration : IEntityTypeConfiguration<AppStageName>
{
    public void Configure(EntityTypeBuilder<AppStageName> builder)
    {
        builder.ToTable("AppStageNames");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasColumnName("StageName").IsRequired().HasMaxLength(64);
        builder.Property(x => x.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name)
               .IsUnique()
               .HasFilter("[IsEnabled] = 1")
               .HasDatabaseName("UX_AppStageNames_StageName");

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
