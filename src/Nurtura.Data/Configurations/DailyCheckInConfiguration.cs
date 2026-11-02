using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class DailyCheckInConfiguration : IEntityTypeConfiguration<DailyCheckIn>
{
    public void Configure(EntityTypeBuilder<DailyCheckIn> builder)
    {
        builder.Property(c => c.Note)
            .HasMaxLength(DailyCheckIn.NoteMaxLength);

        builder.ToTable(t =>
        {
            foreach (var rating in new[]
                     {
                         nameof(DailyCheckIn.Mood), nameof(DailyCheckIn.SleepQuality),
                         nameof(DailyCheckIn.Energy), nameof(DailyCheckIn.Stress)
                     })
            {
                t.HasCheckConstraint(
                    $"CK_DailyCheckIns_{rating}",
                    $"\"{rating}\" BETWEEN {DailyCheckIn.RatingMin} AND {DailyCheckIn.RatingMax}");
            }
        });

        // One check-in per mother per date; deleting the profile deletes her check-ins.
        builder.HasIndex(c => new { c.MotherProfileId, c.Date }).IsUnique();
        builder.HasOne<MotherProfile>()
            .WithMany()
            .HasForeignKey(c => c.MotherProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.FeelingTags)
            .WithOne()
            .HasForeignKey(t => t.DailyCheckInId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
