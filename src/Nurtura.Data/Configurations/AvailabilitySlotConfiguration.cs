using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class AvailabilitySlotConfiguration : IEntityTypeConfiguration<AvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AvailabilitySlots_StartHour",
            $"\"{nameof(AvailabilitySlot.StartHour)}\" BETWEEN {AvailabilitySlot.FirstHour} AND {AvailabilitySlot.LastHour}"));

        // One slot per professional per date and hour; deleting the profile deletes her slots.
        builder.HasIndex(s => new { s.ProfessionalProfileId, s.Date, s.StartHour }).IsUnique();
        builder.HasOne<ProfessionalProfile>()
            .WithMany()
            .HasForeignKey(s => s.ProfessionalProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
