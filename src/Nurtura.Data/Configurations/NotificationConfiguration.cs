using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(n => n.OtherName)
            .IsRequired()
            .HasMaxLength(MotherProfile.DisplayNameMaxLength);

        builder.Property(n => n.Message)
            .HasMaxLength(Appointment.MessageMaxLength);

        builder.Property(n => n.PostTitle)
            .HasMaxLength(ForumPost.TitleMaxLength);

        // Deleting the user, the appointment or the forum post deletes the notification.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(n => n.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ForumPost>()
            .WithMany()
            .HasForeignKey(n => n.ForumPostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting the comment (by its author, the admin, or with the author's account) deletes the notification.
        builder.HasOne<ForumComment>()
            .WithMany()
            .HasForeignKey(n => n.ForumCommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // The bell counts a user's unread ones; the page lists hers newest first.
        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
    }
}
