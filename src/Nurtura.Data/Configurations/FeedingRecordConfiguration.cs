using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class FeedingRecordConfiguration : IEntityTypeConfiguration<FeedingRecord>
{
    public void Configure(EntityTypeBuilder<FeedingRecord> builder)
    {
        builder.Property(f => f.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(f => f.BreastSide).HasConversion<string>().HasMaxLength(30);
        builder.Property(f => f.BottleContent).HasConversion<string>().HasMaxLength(30);
        builder.Property(f => f.Note).HasMaxLength(FeedingRecord.NoteMaxLength);

        // Only the chosen type's fields are filled: breast needs a side, a bottle needs its content,
        // solids have none of them. Minutes and ml are optional, within their ranges.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_FeedingRecords_TypeFields",
                $"""
                ("Type" = '{FeedingType.Breast}' AND "BreastSide" IS NOT NULL AND "AmountMl" IS NULL AND "BottleContent" IS NULL)
                OR ("Type" = '{FeedingType.Bottle}' AND "BottleContent" IS NOT NULL AND "BreastSide" IS NULL AND "Minutes" IS NULL)
                OR ("Type" = '{FeedingType.Solid}' AND "BreastSide" IS NULL AND "Minutes" IS NULL AND "AmountMl" IS NULL AND "BottleContent" IS NULL)
                """);
            t.HasCheckConstraint(
                "CK_FeedingRecords_Minutes",
                $"\"Minutes\" IS NULL OR \"Minutes\" BETWEEN {FeedingRecord.MinutesMin} AND {FeedingRecord.MinutesMax}");
            t.HasCheckConstraint(
                "CK_FeedingRecords_AmountMl",
                $"\"AmountMl\" IS NULL OR \"AmountMl\" BETWEEN {FeedingRecord.AmountMlMin} AND {FeedingRecord.AmountMlMax}");
        });

        builder.HasIndex(f => new { f.BabyId, f.Time });

        // Deleting a baby deletes its feeds.
        builder.HasOne<Baby>()
            .WithMany()
            .HasForeignKey(f => f.BabyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
