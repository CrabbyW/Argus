using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasColumnName("TagName").IsRequired().HasMaxLength(64);
        builder.Property(x => x.Description).HasMaxLength(512);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name)
               .IsUnique()
               .HasFilter("[IsEnabled] = 1")
               .HasDatabaseName("UX_Tags_TagName");

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
