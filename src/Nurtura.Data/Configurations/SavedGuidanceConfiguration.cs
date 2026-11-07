using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class SavedGuidanceConfiguration : IEntityTypeConfiguration<SavedGuidance>
{
    public void Configure(EntityTypeBuilder<SavedGuidance> builder)
    {
        // Each page at most once per mother. Deleting her profile or the page deletes the saved row.
        builder.HasIndex(s => new { s.MotherProfileId, s.GuidanceContentId }).IsUnique();
        builder.HasOne<MotherProfile>()
            .WithMany()
            .HasForeignKey(s => s.MotherProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GuidanceContent>()
            .WithMany()
            .HasForeignKey(s => s.GuidanceContentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
