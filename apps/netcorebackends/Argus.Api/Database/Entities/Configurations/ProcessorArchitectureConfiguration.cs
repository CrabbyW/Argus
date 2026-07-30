using Argus.Api.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class ProcessorArchitectureConfiguration : IEntityTypeConfiguration<ProcessorArchitecture>
{
    public void Configure(EntityTypeBuilder<ProcessorArchitecture> builder)
    {
        builder.ToTable("ProcessorArchitectures");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ArchitectureName).IsRequired().HasMaxLength(32);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.ArchitectureName).IsUnique();

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
