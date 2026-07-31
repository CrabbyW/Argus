using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class ApplicationInstallationConfiguration : IEntityTypeConfiguration<ApplicationInstallation>
{
    public void Configure(EntityTypeBuilder<ApplicationInstallation> builder)
    {
        builder.ToTable("ApplicationInstallations");
        builder.HasKey(x => x.Id);

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

        builder.HasOne(x => x.AppName)
               .WithMany(a => a.Installations)
               .HasForeignKey(x => x.AppNameId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AppStageName)
               .WithMany(s => s.Installations)
               .HasForeignKey(x => x.AppStageNameId)
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

        builder.HasOne(x => x.RootPath)
               .WithMany(p => p.Installations)
               .HasForeignKey(x => x.RootPathId)
               .OnDelete(DeleteBehavior.Restrict);

        // Optional: an installation need not record where it sits on disk.
        builder.HasOne(x => x.PhysicalPath)
               .WithMany(p => p.Installations)
               .HasForeignKey(x => x.PhysicalPathId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);

        // The same app+stage cannot sit twice at the same path on the same machine — but only
        // among rows that are still there. Decommissioning is a soft delete, so without the
        // filter the retired row keeps its slot forever and installing the same thing again
        // (an ordinary event in an inventory, and what ValidFromDate/ValidToDate exist to
        // record) fails on a constraint the user cannot see or clear.
        builder.HasIndex(x => new { x.MachineId, x.AppNameId, x.AppStageNameId, x.RootPathId })
               .IsUnique()
               .HasFilter("[IsEnabled] = 1")
               .HasDatabaseName("UX_ApplicationInstallations_Deployment");

        // Supports the common "what runs on this machine / where is this app" queries.
        builder.HasIndex(x => x.MachineId);
        builder.HasIndex(x => x.AppNameId);
        builder.HasIndex(x => x.DnsEndpointId);
        builder.HasIndex(x => x.RootPathId);
        builder.HasIndex(x => x.PhysicalPathId);

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
