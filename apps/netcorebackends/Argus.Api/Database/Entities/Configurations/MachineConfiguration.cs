using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

/// <summary>
/// The template every lookup configuration follows: the CLR property is <c>Name</c>, the column
/// keeps the name the business uses, and the unique index is filtered on <c>IsEnabled</c> so it
/// agrees with the duplicate check in the lookup service (which only sees live rows). Without the
/// filter, retiring "BOREAS02" and creating it again passes validation and then dies on the index.
/// </summary>
public class MachineConfiguration : IEntityTypeConfiguration<Machine>
{
    public void Configure(EntityTypeBuilder<Machine> builder)
    {
        builder.ToTable("Machines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasColumnName("MachineName").IsRequired().HasMaxLength(128);
        builder.Property(x => x.Description).HasMaxLength(512);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name)
               .IsUnique()
               .HasFilter("[IsEnabled] = 1")
               .HasDatabaseName("UX_Machines_MachineName");

        // Soft delete: disabled rows are invisible to ordinary queries.
        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
