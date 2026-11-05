using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.Property(a => a.ConcernText)
            .IsRequired()
            .HasMaxLength(Appointment.ConcernMaxLength);

        builder.Property(a => a.Topic)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(a => a.CancelledBy)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(a => a.SessionEndedBy)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(a => a.ProfessionalMessage)
            .HasMaxLength(Appointment.MessageMaxLength);

        // Deleting the mother's profile deletes her appointments.
        builder.HasOne<MotherProfile>()
            .WithMany()
            .HasForeignKey(a => a.MotherProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Removing a baby keeps the appointment; it is then no longer about a particular baby.
        builder.HasOne<Baby>()
            .WithMany()
            .HasForeignKey(a => a.BabyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Slots are never deleted (closing sets IsOpen to false), so an appointment always keeps its date and time.
        builder.HasOne<AvailabilitySlot>()
            .WithMany()
            .HasForeignKey(a => a.AvailabilitySlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // Only one active (requested or confirmed) appointment per slot: two mothers racing for
        // the same slot cannot both win.
        var active = string.Join(", ", Appointment.ActiveStatuses.Select(s => $"'{s}'"));
        builder.HasIndex(a => a.AvailabilitySlotId)
            .IsUnique()
            .HasFilter($"\"{nameof(Appointment.Status)}\" IN ({active})")
            .HasDatabaseName("IX_Appointments_AvailabilitySlotId_Active");
    }
}
