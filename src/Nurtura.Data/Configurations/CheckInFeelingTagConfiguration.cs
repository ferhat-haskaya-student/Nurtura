using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class CheckInFeelingTagConfiguration : IEntityTypeConfiguration<CheckInFeelingTag>
{
    public void Configure(EntityTypeBuilder<CheckInFeelingTag> builder)
    {
        // Each tag at most once per check-in. The check-in side is set up in DailyCheckInConfiguration.
        builder.HasKey(t => new { t.DailyCheckInId, t.FeelingTagId });

        // A fixed tag cannot be deleted while check-ins use it.
        builder.HasOne<FeelingTag>()
            .WithMany()
            .HasForeignKey(t => t.FeelingTagId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
