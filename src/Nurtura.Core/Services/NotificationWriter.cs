using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Adds a notification to the context; the caller saves it in the same transaction as the change it reports.
public static class NotificationWriter
{
    public static void Add(
        INurturaDbContext db,
        string userId,
        NotificationType type,
        int appointmentId,
        string otherName,
        DateOnly date,
        int hour,
        string? message = null,
        DateOnly? previousDate = null,
        int? previousHour = null)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            AppointmentId = appointmentId,
            OtherName = otherName,
            Date = date,
            Hour = hour,
            PreviousDate = previousDate,
            PreviousHour = previousHour,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });
    }
}
