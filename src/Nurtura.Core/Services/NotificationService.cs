using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Reading notifications. Every method takes the logged-in user's id and works only on her own notifications.
public class NotificationService
{
    private readonly INurturaDbContext _db;

    public NotificationService(INurturaDbContext db)
    {
        _db = db;
    }

    // For the bell, on every page load. The expiry sweep runs first, so a "not confirmed in time"
    // notification is there as soon as it is true.
    public async Task<int> CountUnreadAsync(string userId, CancellationToken cancellationToken = default)
    {
        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);
        return await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }

    // Newest first. Read notifications older than 30 days are deleted first.
    public async Task<IReadOnlyList<Notification>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Notification.KeepReadDays);
        await _db.Notifications
            .Where(n => n.UserId == userId && n.IsRead && n.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        return await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .ToListAsync(cancellationToken);
    }

    // Marks one of hers as read and returns its appointment id and type; null when it is not hers.
    public async Task<(int AppointmentId, NotificationType Type)?> OpenAsync(
        string userId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var found = await _db.Notifications
            .Where(n => n.Id == id && n.UserId == userId)
            .Select(n => new { n.AppointmentId, n.Type })
            .SingleOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return null;
        }

        await _db.Notifications
            .Where(n => n.Id == id && n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.IsRead, true), cancellationToken);
        return (found.AppointmentId, found.Type);
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.IsRead, true), cancellationToken);
    }
}
