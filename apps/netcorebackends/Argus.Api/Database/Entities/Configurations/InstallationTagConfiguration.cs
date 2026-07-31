using Argus.Api.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class InstallationTagConfiguration : IEntityTypeConfiguration<InstallationTag>
{
    public void Configure(EntityTypeBuilder<InstallationTag> builder)
    {
        builder.ToTable("InstallationTags");
        builder.HasKey(x => new { x.InstallationId, x.TagId });

        // Removing an installation for good takes its links with it.
        builder.HasOne(x => x.Installation)
               .WithMany(i => i.InstallationTags)
               .HasForeignKey(x => x.InstallationId)
               .OnDelete(DeleteBehavior.Cascade);

        // A tag still linked to an installation cannot be hard-deleted, matching the rule every
        // other lookup follows.
        builder.HasOne(x => x.Tag)
               .WithMany(t => t.InstallationTags)
               .HasForeignKey(x => x.TagId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TagId);

        // No HasQueryFilter here on purpose — see the note on InstallationTag itself.
    }
}
