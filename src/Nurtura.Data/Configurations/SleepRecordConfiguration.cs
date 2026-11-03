using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class SleepRecordConfiguration : IEntityTypeConfiguration<SleepRecord>
{
    public void Configure(EntityTypeBuilder<SleepRecord> builder)
    {
        builder.Property(s => s.Note)
            .HasMaxLength(SleepRecord.NoteMaxLength);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_SleepRecords_EndAfterStart",
            "\"EndTime\" IS NULL OR \"EndTime\" > \"StartTime\""));

        builder.HasIndex(s => new { s.BabyId, s.StartTime });

        // One running sleep per baby, guarded by the database too.
        builder.HasIndex(s => s.BabyId)
            .IsUnique()
            .HasFilter("\"EndTime\" IS NULL")
            .HasDatabaseName("IX_SleepRecords_BabyId_Running");

        // Deleting a baby deletes its sleeps.
        builder.HasOne<Baby>()
            .WithMany()
            .HasForeignKey(s => s.BabyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
