using Argus.Api.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class AppStageConfiguration : IEntityTypeConfiguration<AppStage>
{
    public void Configure(EntityTypeBuilder<AppStage> builder)
    {
        builder.ToTable("AppStages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StageName).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.StageName).IsUnique();

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
