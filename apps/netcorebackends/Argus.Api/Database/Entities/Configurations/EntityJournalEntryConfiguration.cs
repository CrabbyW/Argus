using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Api.Database.Entities.Configurations;

public class EntityJournalEntryConfiguration : IEntityTypeConfiguration<EntityJournalEntry>
{
    public void Configure(EntityTypeBuilder<EntityJournalEntry> builder)
    {
        // Singular, as the table was named in the request.
        builder.ToTable("EntityJournal");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ChangeSetId).IsRequired();
        builder.Property(x => x.EntityName).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Field).HasMaxLength(64);

        // 512 is generous for a lookup name and still bounded — a repository url is the longest
        // value that realistically lands here.
        builder.Property(x => x.OldValue).HasMaxLength(512);
        builder.Property(x => x.NewValue).HasMaxLength(512);

        builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ChangedUtc).IsRequired();

        // Cascade, unlike the Restrict used for lookups: a journal row has no meaning without the
        // installation it describes. In practice it almost never fires — installations are
        // soft-deleted — and it exists for a genuine hard delete.
        builder.HasOne(x => x.Installation)
               .WithMany(i => i.JournalEntries)
               .HasForeignKey(x => x.InstallationId)
               .OnDelete(DeleteBehavior.Cascade);

        // The only query this table serves: one installation's history, newest first.
        builder.HasIndex(x => new { x.InstallationId, x.ChangedUtc })
               .HasDatabaseName("IX_EntityJournal_Installation");

        // No HasQueryFilter, on purpose — see the note on the entity. An audit row is never
        // hidden, so there is nothing for a filter to do here.
    }
}
