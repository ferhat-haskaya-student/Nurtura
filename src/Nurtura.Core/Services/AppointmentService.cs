using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Appointment lists and status changes for both sides. Every method takes the logged-in user's id and
// works only on her own appointments: a mother's through her profile, a professional's through her slots.
// Each change is one conditional UPDATE (owner, expected status, still in time), so a change that is no
// longer allowed, or that the other side made first, changes nothing and returns false.
// Cancelling and rescheduling stop when the chat room opens (5 minutes before the start); the note stays
// editable until the start.
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

    // One of her appointments that has not started and is still requested or confirmed (her note can still be
    // edited); null otherwise.
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
        return item is { CanEditNote: true } ? item : null;
    }

    // She cancels a requested or confirmed appointment whose chat room has not opened. Frees the slot.
    public async Task<bool> CancelByMotherAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var motherId = await GetMotherIdAsync(userId, cancellationToken);
        if (motherId is null)
        {
            return false;
        }

        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        // Who to tell: the professional of the slot.
        var details = await (
                from a in _db.Appointments
                where a.Id == id && a.MotherProfileId == motherId
                join s in _db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
                join p in _db.ProfessionalProfiles on s.ProfessionalProfileId equals p.Id
                join m in _db.MotherProfiles on a.MotherProfileId equals m.Id
                select new { p.UserId, MotherName = m.DisplayName, s.Date, s.StartHour })
            .SingleOrDefaultAsync(cancellationToken);
        if (details is null)
        {
            return false;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var (today, hour) = ConsultationRoom.OpeningCutoff(PolandTime.Now);
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
        if (changed != 1)
        {
            return false;
        }

        NotificationWriter.Add(
            _db, details.UserId, NotificationType.CancelledByMother, id, details.MotherName, details.Date, details.StartHour);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
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

    // One of her appointments in the given stored status whose chat room has not opened; null otherwise.
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
        return item is { CanCancel: true } ? item : null;
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

        // Who to tell: the mother.
        var details = await (
                from a in _db.Appointments
                where a.Id == id
                join s in _db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
                where s.ProfessionalProfileId == professionalId
                join p in _db.ProfessionalProfiles on s.ProfessionalProfileId equals p.Id
                join m in _db.MotherProfiles on a.MotherProfileId equals m.Id
                select new { m.UserId, ProfessionalName = p.DisplayName, s.Date, s.StartHour })
            .SingleOrDefaultAsync(cancellationToken);
        if (details is null)
        {
            return false;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var (today, hour) = ConsultationRoom.OpeningCutoff(PolandTime.Now);
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
        if (changed != 1)
        {
            return false;
        }

        var type = to switch
        {
            AppointmentStatus.Confirmed => NotificationType.RequestAccepted,
            AppointmentStatus.Rejected => NotificationType.RequestRejected,
            _ => NotificationType.CancelledByProfessional
        };
        NotificationWriter.Add(
            _db, details.UserId, type, id, details.ProfessionalName, details.Date, details.StartHour, text);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // The banner on her dashboard and My appointments: her confirmed consultations from 24 hours before
    // the start until the end (end of the hour, or End session). Worked out from the clock, never stored.
    public async Task<IReadOnlyList<ConsultationBanner>> GetMotherBannersAsync(string userId, CancellationToken cancellationToken = default)
    {
        var motherId = await GetMotherIdAsync(userId, cancellationToken);
        return motherId is null ? [] : await BannersAsync(QueryMine(motherId.Value), cancellationToken);
    }

    // The same for a professional: confirmed consultations on her slots.
    public async Task<IReadOnlyList<ConsultationBanner>> GetProfessionalBannersAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var professionalId = await GetProfessionalIdAsync(userId, cancellationToken);
        return professionalId is null ? [] : await BannersAsync(QueryProfessional(professionalId.Value), cancellationToken);
    }

    private static async Task<IReadOnlyList<ConsultationBanner>> BannersAsync(IQueryable<Row> rows, CancellationToken cancellationToken)
    {
        var now = PolandTime.Now;
        var today = DateOnly.FromDateTime(now);
        var tomorrow = today.AddDays(1);
        var candidates = await rows
            .Where(r => r.Status == AppointmentStatus.Confirmed && r.Date >= today && r.Date <= tomorrow)
            .ToListAsync(cancellationToken);

        return candidates
            .Select(r => (Row: r, Start: ConsultationRoom.Start(r.Date, r.Hour), OpensAt: ConsultationRoom.OpensAt(r.Date, r.Hour)))
            .Where(x => x.Start - TimeSpan.FromHours(24) <= now
                && now < ConsultationRoom.End(x.Row.Date, x.Row.Hour, x.Row.SessionEndedAt))
            .OrderBy(x => x.Start)
            .Select(x => new ConsultationBanner(
                x.Row.Id, x.Row.OtherName, x.Row.Hour, x.Start <= now, x.Row.Date == today, x.OpensAt <= now, x.OpensAt))
            .ToList();
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

    // A confirmed appointment is Done from its real end: the end of the hour, or earlier after End session.
    private static AppointmentState StateOf(Row r, DateTime now)
    {
        var start = ConsultationRoom.Start(r.Date, r.Hour);
        return r.Status switch
        {
            AppointmentStatus.Requested => AppointmentState.Requested,
            AppointmentStatus.Confirmed when ConsultationRoom.End(r.Date, r.Hour, r.SessionEndedAt) <= now => AppointmentState.Done,
            AppointmentStatus.Confirmed when start <= now => AppointmentState.Now,
            AppointmentStatus.Confirmed => AppointmentState.Confirmed,
            AppointmentStatus.Completed => AppointmentState.Done,
            AppointmentStatus.Rejected => AppointmentState.Rejected,
            AppointmentStatus.Cancelled => AppointmentState.Cancelled,
            _ => AppointmentState.Expired
        };
    }

    // Cancel and Reschedule: until the chat room opens.
    private static bool CanCancel(AppointmentState state, Row r, DateTime now) =>
        state is AppointmentState.Requested or AppointmentState.Confirmed && ConsultationRoom.BeforeOpening(r.Date, r.Hour, now);

    // Edit note: until the start.
    private static bool CanEditNote(AppointmentState state, Row r, DateTime now) =>
        state is AppointmentState.Requested or AppointmentState.Confirmed && ConsultationRoom.Start(r.Date, r.Hour) > now;

    // Only a confirmed appointment has a room. After the session (or once completed) it is read-only.
    private static ChatAccess ChatOf(Row r, DateTime now) => r.Status switch
    {
        AppointmentStatus.Confirmed => ConsultationRoom.StateOf(r.Date, r.Hour, r.SessionEndedAt, now) switch
        {
            RoomState.NotYetOpen => ChatAccess.NotYetOpen,
            RoomState.Open => ChatAccess.Open,
            _ => ChatAccess.ViewOnly
        },
        AppointmentStatus.Completed => ChatAccess.ViewOnly,
        _ => ChatAccess.None
    };

    private static MotherAppointment ToMother(Row r, DateTime now)
    {
        var state = StateOf(r, now);
        return new MotherAppointment(
            r.Id, r.Date, r.Hour, r.Topic, r.OtherName, r.Concern, state, r.CancelledBy, r.Message,
            BookingService.ConfirmBy(r.Date, r.Hour), CanCancel(state, r, now), CanEditNote(state, r, now),
            ChatOf(r, now), ConsultationRoom.OpensAt(r.Date, r.Hour));
    }

    private static ProfessionalAppointment ToProfessional(Row r, DateTime now)
    {
        var state = StateOf(r, now);
        return new ProfessionalAppointment(
            r.Id, r.Date, r.Hour, r.Topic, r.OtherName, r.Concern, state, r.CancelledBy, r.Message,
            BookingService.ConfirmBy(r.Date, r.Hour), CanCancel(state, r, now), CanEditNote(state, r, now),
            ChatOf(r, now), ConsultationRoom.OpensAt(r.Date, r.Hour),
            r.Status == AppointmentStatus.Confirmed && state == AppointmentState.Done);
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
            Concern = a.ConcernText, Status = a.Status, CancelledBy = a.CancelledBy, Message = a.ProfessionalMessage,
            SessionEndedAt = a.SessionEndedAt
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
            Concern = a.ConcernText, Status = a.Status, CancelledBy = a.CancelledBy, Message = a.ProfessionalMessage,
            SessionEndedAt = a.SessionEndedAt
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
        public DateTime? SessionEndedAt { get; init; }
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
