using Argus.Api.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class InstallationConfiguration : IEntityTypeConfiguration<Installation>
{
    public void Configure(EntityTypeBuilder<Installation> builder)
    {
        builder.ToTable("Installations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RootPath).IsRequired().HasMaxLength(256);
        builder.Property(x => x.PhysicalPath).HasMaxLength(512);
        builder.Property(x => x.Tags).HasMaxLength(512);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.ValidFromDate).IsRequired().HasColumnType("date");
        builder.Property(x => x.ValidToDate).HasColumnType("date");
        builder.Property(x => x.CreatedUtc).IsRequired();

        // Lookups are referenced, never duplicated. Restrict: a lookup still in use
        // cannot be hard-deleted out from under an installation.
        builder.HasOne(x => x.Machine)
               .WithMany(m => m.Installations)
               .HasForeignKey(x => x.MachineId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Application)
               .WithMany(a => a.Installations)
               .HasForeignKey(x => x.ApplicationId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AppStage)
               .WithMany(s => s.Installations)
               .HasForeignKey(x => x.AppStageId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProcessorArchitecture)
               .WithMany(p => p.Installations)
               .HasForeignKey(x => x.ProcessorArchitectureId)
               .OnDelete(DeleteBehavior.Restrict);

        // Optional: a service/console installation has no DNS name.
        builder.HasOne(x => x.DnsEndpoint)
               .WithMany(d => d.Installations)
               .HasForeignKey(x => x.DnsEndpointId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);

        // The same app+stage cannot sit twice at the same path on the same machine.
        builder.HasIndex(x => new { x.MachineId, x.ApplicationId, x.AppStageId, x.RootPath })
               .IsUnique()
               .HasDatabaseName("UX_Installations_Deployment");

        // Supports the common "what runs on this machine / where is this app" queries.
        builder.HasIndex(x => x.MachineId);
        builder.HasIndex(x => x.ApplicationId);
        builder.HasIndex(x => x.DnsEndpointId);

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
