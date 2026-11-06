using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class ConsultationNoteConfiguration : IEntityTypeConfiguration<ConsultationNote>
{
    public void Configure(EntityTypeBuilder<ConsultationNote> builder)
    {
        builder.Property(n => n.Text)
            .IsRequired()
            .HasMaxLength(ConsultationNote.TextMaxLength);

        // Deleting the appointment (for example with the mother's account) deletes its note.
        builder.HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(n => n.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // One note per appointment; also makes a second "first save" at the same moment fail.
        builder.HasIndex(n => n.AppointmentId)
            .IsUnique();
    }
}
