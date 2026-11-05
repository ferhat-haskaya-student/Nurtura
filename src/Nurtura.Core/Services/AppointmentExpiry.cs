using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// A request the professional has not confirmed by 2 hours before the start expires and frees the slot.
// There is no background job: every service that reads or books slots calls ExpireOverdueAsync first,
// so the data is correct whenever it is read and the one-active-appointment-per-slot index never
// sees an overdue request.
public static class AppointmentExpiry
{
    public static async Task ExpireOverdueAsync(INurturaDbContext db, CancellationToken cancellationToken = default)
    {
        // Slots are stored in Poland time on the full hour, so "starts at or before the cutoff"
        // is: an earlier date, or the same date and an hour not after the cutoff's hour.
        var cutoff = PolandTime.Now + Appointment.ConfirmBefore;
        var cutoffDate = DateOnly.FromDateTime(cutoff);
        var cutoffHour = cutoff.Hour;
        var utcNow = DateTime.UtcNow;

        await db.Appointments
            .Where(a => a.Status == AppointmentStatus.Requested
                && db.AvailabilitySlots.Any(s => s.Id == a.AvailabilitySlotId
                    && (s.Date < cutoffDate || (s.Date == cutoffDate && s.StartHour <= cutoffHour))))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(a => a.Status, AppointmentStatus.Expired)
                    .SetProperty(a => a.UpdatedAt, utcNow),
                cancellationToken);
    }
}
