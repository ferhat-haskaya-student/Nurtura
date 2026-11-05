using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.Property(m => m.Text)
            .IsRequired()
            .HasMaxLength(ChatMessage.TextMaxLength);

        builder.Property(m => m.Sender)
            .HasConversion<string>()
            .HasMaxLength(30);

        // Deleting the appointment (for example with the mother's account) deletes its chat.
        builder.HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(m => m.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // The room loads its messages oldest first.
        builder.HasIndex(m => new { m.AppointmentId, m.SentAt });
    }
}
