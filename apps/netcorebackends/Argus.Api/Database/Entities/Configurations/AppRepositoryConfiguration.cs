using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class AppRepositoryConfiguration : IEntityTypeConfiguration<AppRepository>
{
    public void Configure(EntityTypeBuilder<AppRepository> builder)
    {
        builder.ToTable("AppRepositories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasColumnName("RepositoryUrl").IsRequired().HasMaxLength(512);
        // Restrict, not Cascade or SetNull: deleting a type that repositories still point at must
        // fail loudly rather than quietly rewriting their type. The lookup layer refuses it before
        // the database ever sees it (see LookupRegistry), and this is the backstop.
        builder.HasOne(x => x.RepositoryType)
               .WithMany(x => x.AppRepositories)
               .HasForeignKey(x => x.RepositoryTypeId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Description).HasMaxLength(512);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        // The url identifies the repository, so it is unique across the whole table now that
        // repositories are no longer scoped to one application.
        builder.HasIndex(x => x.Name)
               .IsUnique()
               .HasFilter("[IsEnabled] = 1")
               .HasDatabaseName("UX_AppRepositories_RepositoryUrl");

        builder.HasQueryFilter(x => x.IsEnabled);
    }
}
