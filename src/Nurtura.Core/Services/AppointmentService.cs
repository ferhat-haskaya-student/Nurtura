using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Appointment lists and status changes for both sides. Every method takes the logged-in user's id and
// works only on her own appointments: a mother's through her profile, a professional's through her slots.
// Each change is one conditional UPDATE (owner, expected status, not yet started), so a change that is no
// longer allowed, or that the other side made first, changes nothing and returns false.
public class AppointmentService
{
    private readonly INurturaDbContext _db;

    public AppointmentService(INurturaDbContext db)
    {
        _db = db;
    }

    // Null when the user has no mother profile.
    public async Task<MotherAppointments?> GetMineAsync(string userId, CancellationToken cancellationToken = default)
    {
        var motherId = await GetMotherIdAsync(userId, cancellationToken);
        if (motherId is null)
        {
            return null;
        }

        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        var now = PolandTime.Now;
        var items = (await QueryMine(motherId.Value).ToListAsync(cancellationToken))
            .Select(r => ToMother(r, now))
            .ToList();

        return new MotherAppointments(
            items.Where(IsUpcoming).OrderBy(a => a.Date).ThenBy(a => a.Hour).ToList(),
            items.Where(a => !IsUpcoming(a)).OrderByDescending(a => a.Date).ThenByDescending(a => a.Hour).ToList());
    }

    // One of her appointments that has not started and is still requested or confirmed; null otherwise.
    public async Task<MotherAppointment?> GetChangeableAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var motherId = await GetMotherIdAsync(userId, cancellationToken);
        if (motherId is null)
        {
            return null;
        }

        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        var now = PolandTime.Now;
        var row = await QueryMine(motherId.Value).Where(r => r.Id == id).SingleOrDefaultAsync(cancellationToken);
        var item = row is null ? null : ToMother(row, now);
        return item is { CanChange: true } ? item : null;
    }

    // She cancels a requested or confirmed appointment that has not started. Frees the slot.
    public async Task<bool> CancelByMotherAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var motherId = await GetMotherIdAsync(userId, cancellationToken);
        if (motherId is null)
        {
            return false;
        }

        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        var (today, hour) = NowParts();
        var utcNow = DateTime.UtcNow;
        var changed = await _db.Appointments
            .Where(a => a.Id == id && a.MotherProfileId == motherId && Appointment.ActiveStatuses.Contains(a.Status)
                && _db.AvailabilitySlots.Any(s => s.Id == a.AvailabilitySlotId
                    && (s.Date > today || (s.Date == today && s.StartHour > hour))))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(a => a.Status, AppointmentStatus.Cancelled)
                    .SetProperty(a => a.CancelledBy, AppointmentParty.Mother)
                    .SetProperty(a => a.UpdatedAt, utcNow),
                cancellationToken);
        return changed == 1;
    }

    // She edits her note on a requested or confirmed appointment until it starts.
    // The text is normalized like the request: "\r\n" becomes "\n", then 1 to 500 characters.
    public async Task<bool> EditNoteAsync(string userId, int id, string concern, CancellationToken cancellationToken = default)
    {
        var text = concern.ReplaceLineEndings("\n").Trim();
        if (text.Length == 0 || text.Length > Appointment.ConcernMaxLength)
        {
            return false;
        }

        var motherId = await GetMotherIdAsync(userId, cancellationToken);
        if (motherId is null)
        {
            return false;
        }

        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        var (today, hour) = NowParts();
        var utcNow = DateTime.UtcNow;
        var changed = await _db.Appointments
            .Where(a => a.Id == id && a.MotherProfileId == motherId && Appointment.ActiveStatuses.Contains(a.Status)
                && _db.AvailabilitySlots.Any(s => s.Id == a.AvailabilitySlotId
                    && (s.Date > today || (s.Date == today && s.StartHour > hour))))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(a => a.ConcernText, text)
                    .SetProperty(a => a.UpdatedAt, utcNow),
                cancellationToken);
        return changed == 1;
    }

    // Null when the user has no approved professional profile.
    public async Task<ProfessionalAppointments?> GetForProfessionalAsync(string userId, CancellationToken cancellationToken = default)
    {
        var professionalId = await GetProfessionalIdAsync(userId, cancellationToken);
        if (professionalId is null)
        {
            return null;
        }

        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        var now = PolandTime.Now;
        var items = (await QueryProfessional(professionalId.Value).ToListAsync(cancellationToken))
            .Select(r => ToProfessional(r, now))
            .ToList();

        return new ProfessionalAppointments(
            items.Where(a => a.State == AppointmentState.Requested).OrderBy(a => a.Date).ThenBy(a => a.Hour).ToList(),
            items.Where(a => a.State is AppointmentState.Confirmed or AppointmentState.Now)
                .OrderBy(a => a.Date).ThenBy(a => a.Hour).ToList(),
            items.Where(a => a.State is not (AppointmentState.Requested or AppointmentState.Confirmed or AppointmentState.Now))
                .OrderByDescending(a => a.Date).ThenByDescending(a => a.Hour).ToList());
    }

    // One of her appointments in the given stored status that has not started; null otherwise.
    public async Task<ProfessionalAppointment?> GetForProfessionalAsync(
        string userId,
        int id,
        AppointmentStatus status,
        CancellationToken cancellationToken = default)
    {
        var professionalId = await GetProfessionalIdAsync(userId, cancellationToken);
        if (professionalId is null)
        {
            return null;
        }

        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        var now = PolandTime.Now;
        var row = await QueryProfessional(professionalId.Value)
            .Where(r => r.Id == id && r.Status == status)
            .SingleOrDefaultAsync(cancellationToken);
        var item = row is null ? null : ToProfessional(row, now);
        return item is { CanChange: true } ? item : null;
    }

    // Requested -> Confirmed. An expired request can't be accepted: the expiry sweep runs first.
    public Task<bool> AcceptAsync(string userId, int id, CancellationToken cancellationToken = default) =>
        ChangeByProfessionalAsync(userId, id, AppointmentStatus.Requested, AppointmentStatus.Confirmed, null, cancellationToken);

    // Requested -> Rejected, with an optional message to the mother. Frees the slot.
    public Task<bool> RejectAsync(string userId, int id, string? message, CancellationToken cancellationToken = default) =>
        ChangeByProfessionalAsync(userId, id, AppointmentStatus.Requested, AppointmentStatus.Rejected, message, cancellationToken);

    // Confirmed -> Cancelled by the professional, with an optional message to the mother. Frees the slot.
    public Task<bool> CancelByProfessionalAsync(string userId, int id, string? message, CancellationToken cancellationToken = default) =>
        ChangeByProfessionalAsync(userId, id, AppointmentStatus.Confirmed, AppointmentStatus.Cancelled, message, cancellationToken);

    private async Task<bool> ChangeByProfessionalAsync(
        string userId,
        int id,
        AppointmentStatus from,
        AppointmentStatus to,
        string? message,
        CancellationToken cancellationToken)
    {
        var text = message?.ReplaceLineEndings("\n").Trim();
        if (string.IsNullOrEmpty(text))
        {
            text = null;
        }
        else if (text.Length > Appointment.MessageMaxLength)
        {
            return false;
        }

        var professionalId = await GetProfessionalIdAsync(userId, cancellationToken);
        if (professionalId is null)
        {
            return false;
        }

        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        var (today, hour) = NowParts();
        var utcNow = DateTime.UtcNow;
        AppointmentParty? cancelledBy = to == AppointmentStatus.Cancelled ? AppointmentParty.Professional : null;
        var changed = await _db.Appointments
            .Where(a => a.Id == id && a.Status == from
                && _db.AvailabilitySlots.Any(s => s.Id == a.AvailabilitySlotId && s.ProfessionalProfileId == professionalId
                    && (s.Date > today || (s.Date == today && s.StartHour > hour))))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(a => a.Status, to)
                    .SetProperty(a => a.CancelledBy, cancelledBy)
                    .SetProperty(a => a.ProfessionalMessage, text)
                    .SetProperty(a => a.UpdatedAt, utcNow),
                cancellationToken);
        return changed == 1;
    }

    private static bool IsUpcoming(MotherAppointment a) =>
        a.State is AppointmentState.Requested or AppointmentState.Confirmed or AppointmentState.Now;

    // Today's date and hour in Poland: a slot has not started when its date is later, or it is today
    // and its hour is later (slots start on the full hour).
    private static (DateOnly Today, int Hour) NowParts()
    {
        var now = PolandTime.Now;
        return (DateOnly.FromDateTime(now), now.Hour);
    }

    private static AppointmentState StateOf(AppointmentStatus status, DateOnly date, int hour, DateTime now)
    {
        var start = BookingService.SlotStart(date, hour);
        return status switch
        {
            AppointmentStatus.Requested => AppointmentState.Requested,
            AppointmentStatus.Confirmed when start + Appointment.Length <= now => AppointmentState.Done,
            AppointmentStatus.Confirmed when start <= now => AppointmentState.Now,
            AppointmentStatus.Confirmed => AppointmentState.Confirmed,
            AppointmentStatus.Completed => AppointmentState.Done,
            AppointmentStatus.Rejected => AppointmentState.Rejected,
            AppointmentStatus.Cancelled => AppointmentState.Cancelled,
            _ => AppointmentState.Expired
        };
    }

    private static bool CanChange(AppointmentState state, DateOnly date, int hour, DateTime now) =>
        state is AppointmentState.Requested or AppointmentState.Confirmed && BookingService.SlotStart(date, hour) > now;

    private static MotherAppointment ToMother(Row r, DateTime now)
    {
        var state = StateOf(r.Status, r.Date, r.Hour, now);
        return new MotherAppointment(
            r.Id, r.Date, r.Hour, r.Topic, r.OtherName, r.Concern, state, r.CancelledBy, r.Message,
            BookingService.ConfirmBy(r.Date, r.Hour), CanChange(state, r.Date, r.Hour, now));
    }

    private static ProfessionalAppointment ToProfessional(Row r, DateTime now)
    {
        var state = StateOf(r.Status, r.Date, r.Hour, now);
        return new ProfessionalAppointment(
            r.Id, r.Date, r.Hour, r.Topic, r.OtherName, r.Concern, state, r.CancelledBy, r.Message,
            BookingService.ConfirmBy(r.Date, r.Hour), CanChange(state, r.Date, r.Hour, now));
    }

    // Her appointments with the professional's display name.
    private IQueryable<Row> QueryMine(int motherId) =>
        from a in _db.Appointments
        where a.MotherProfileId == motherId
        join s in _db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
        join p in _db.ProfessionalProfiles on s.ProfessionalProfileId equals p.Id
        select new Row
        {
            Id = a.Id, Date = s.Date, Hour = s.StartHour, Topic = a.Topic, OtherName = p.DisplayName,
            Concern = a.ConcernText, Status = a.Status, CancelledBy = a.CancelledBy, Message = a.ProfessionalMessage
        };

    // Appointments on her slots with only the mother's display name.
    private IQueryable<Row> QueryProfessional(int professionalId) =>
        from a in _db.Appointments
        join s in _db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
        where s.ProfessionalProfileId == professionalId
        join m in _db.MotherProfiles on a.MotherProfileId equals m.Id
        select new Row
        {
            Id = a.Id, Date = s.Date, Hour = s.StartHour, Topic = a.Topic, OtherName = m.DisplayName,
            Concern = a.ConcernText, Status = a.Status, CancelledBy = a.CancelledBy, Message = a.ProfessionalMessage
        };

    // Object-initializer form, so EF can still filter on it after the projection.
    private sealed class Row
    {
        public int Id { get; init; }
        public DateOnly Date { get; init; }
        public int Hour { get; init; }
        public Topic Topic { get; init; }
        public string OtherName { get; init; } = string.Empty;
        public string Concern { get; init; } = string.Empty;
        public AppointmentStatus Status { get; init; }
        public AppointmentParty? CancelledBy { get; init; }
        public string? Message { get; init; }
    }

    private async Task<int?> GetMotherIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _db.MotherProfiles
            .Where(m => m.UserId == userId)
            .Select(m => (int?)m.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<int?> GetProfessionalIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _db.ProfessionalProfiles
            .Where(p => p.UserId == userId && p.ApprovalStatus == ApprovalStatus.Approved)
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
