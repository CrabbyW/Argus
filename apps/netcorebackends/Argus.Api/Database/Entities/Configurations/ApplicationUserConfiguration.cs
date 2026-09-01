using Argus.Api.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("ApplicationUsers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Username).IsRequired().HasMaxLength(128);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(256);
        // Nullable since Windows sign-in: an account mapped to a domain user has no password.
        builder.Property(x => x.PasswordHash).HasMaxLength(256);
        builder.Property(x => x.PasswordSalt).HasMaxLength(256);
        builder.Property(x => x.WindowsAccountName).HasMaxLength(256);
        builder.Property(x => x.LastLoginMethod).HasMaxLength(16);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CreatedUtc).IsRequired();

        builder.HasIndex(x => x.Username).IsUnique();

        // Filtered: one domain account maps to at most one Argus user, but the many users with no
        // mapping at all are not duplicates of each other — SQL Server's unique index would treat
        // their NULLs as one value without this.
        builder
            .HasIndex(x => x.WindowsAccountName)
            .IsUnique()
            .HasFilter("[WindowsAccountName] IS NOT NULL");

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
