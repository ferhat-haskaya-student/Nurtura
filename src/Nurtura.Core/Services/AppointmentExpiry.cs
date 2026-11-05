using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// A request the professional has not confirmed by 2 hours before the start expires and frees the slot,
// and the mother gets a "not confirmed in time" notification.
// There is no background job: every service that reads or books slots (and the notification bell) calls
// ExpireOverdueAsync first, so the data is correct whenever it is read and the one-active-appointment-per-slot
// index never sees an overdue request.
public static class AppointmentExpiry
{
    public static async Task ExpireOverdueAsync(INurturaDbContext db, CancellationToken cancellationToken = default)
    {
        // Slots are stored in Poland time on the full hour, so "starts at or before the cutoff"
        // is: an earlier date, or the same date and an hour not after the cutoff's hour.
        var cutoff = PolandTime.Now + Appointment.ConfirmBefore;
        var cutoffDate = DateOnly.FromDateTime(cutoff);
        var cutoffHour = cutoff.Hour;

        var overdue = await (
                from a in db.Appointments
                where a.Status == AppointmentStatus.Requested
                join s in db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
                where s.Date < cutoffDate || (s.Date == cutoffDate && s.StartHour <= cutoffHour)
                join m in db.MotherProfiles on a.MotherProfileId equals m.Id
                join p in db.ProfessionalProfiles on s.ProfessionalProfileId equals p.Id
                orderby a.Id
                select new { a.Id, m.UserId, p.DisplayName, s.Date, s.StartHour })
            .ToListAsync(cancellationToken);
        if (overdue.Count == 0)
        {
            return;
        }

        // Inside a caller's transaction (booking) the work joins it; otherwise it gets its own.
        var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var utcNow = DateTime.UtcNow;
            foreach (var row in overdue)
            {
                // Only the sweep that actually changes the row notifies, so two sweeps at once can't notify twice.
                var changed = await db.Appointments
                    .Where(a => a.Id == row.Id && a.Status == AppointmentStatus.Requested)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(a => a.Status, AppointmentStatus.Expired)
                            .SetProperty(a => a.UpdatedAt, utcNow),
                        cancellationToken);
                if (changed == 1)
                {
                    NotificationWriter.Add(
                        db, row.UserId, NotificationType.RequestExpired, row.Id, row.DisplayName, row.Date, row.StartHour);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
