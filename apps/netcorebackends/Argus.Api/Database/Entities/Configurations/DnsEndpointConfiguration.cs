using Argus.Api.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class DnsEndpointConfiguration : IEntityTypeConfiguration<DnsEndpoint>
{
    public void Configure(EntityTypeBuilder<DnsEndpoint> builder)
    {
        builder.ToTable("DnsEndpoints");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DnsName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.IsLoadBalancer).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.Description).HasMaxLength(512);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.DnsName).IsUnique();

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
