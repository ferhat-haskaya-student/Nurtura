using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The mother's side of booking: free slots by topic, professional profiles, and sending a request.
// Methods that take a userId work only with the logged-in mother's own profile, babies and records.
public class BookingService
{
    private readonly INurturaDbContext _db;

    public BookingService(INurturaDbContext db)
    {
        _db = db;
    }

    public static bool IsBabyTopic(Topic topic) => topic is Topic.BabySleep or Topic.BabyFeeding;

    public static DateTime SlotStart(DateOnly date, int hour) => date.ToDateTime(new TimeOnly(hour, 0));

    // Poland time she has to wait for: the professional must confirm by 2 hours before the start.
    public static DateTime ConfirmBy(DateOnly date, int hour) => SlotStart(date, hour) - Appointment.ConfirmBefore;

    // Free slots for the topic, grouped by day: open, of an approved professional who covers the topic
    // (and speaks the language, when one is chosen), starting 12 hours to 4 weeks from now, with no
    // active appointment, and not at an hour where she already has an appointment.
    // Null when the user has no mother profile.
    public async Task<IReadOnlyList<FreeSlotDay>?> GetFreeSlotsAsync(
        string userId,
        Topic topic,
        Language? language,
        CancellationToken cancellationToken = default)
    {
        var motherId = await GetMotherIdAsync(userId, cancellationToken);
        if (motherId is null)
        {
            return null;
        }

        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        var now = PolandTime.Now;
        var busy = (await GetActiveAsync(motherId.Value, cancellationToken)).Select(a => (a.Date, a.Hour)).ToHashSet();
        var slots = await GetCandidateSlotsAsync(topic, language, null, now, cancellationToken);

        return slots
            .Where(s => !busy.Contains((s.Date, s.Hour)))
            .GroupBy(s => s.Date)
            .Select(g => new FreeSlotDay(g.Key, g.ToList()))
            .ToList();
    }

    // An approved professional's profile; null for any other id.
    public async Task<ProfessionalDetails?> GetProfessionalAsync(int profileId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.ProfessionalProfiles
            .Where(p => p.Id == profileId && p.ApprovalStatus == ApprovalStatus.Approved)
            .Select(p => new { p.Id, p.DisplayName, p.ProfessionalType, p.Bio })
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var topics = await _db.ProfessionalCategories
            .Where(c => c.ProfessionalProfileId == profileId)
            .Select(c => c.Category)
            .ToListAsync(cancellationToken);
        var languages = await _db.ProfessionalLanguages
            .Where(l => l.ProfessionalProfileId == profileId)
            .OrderBy(l => l.Id)
            .Select(l => l.Language)
            .ToListAsync(cancellationToken);

        return new ProfessionalDetails(
            profile.Id,
            profile.DisplayName,
            profile.ProfessionalType,
            topics.Order().ToList(),
            languages,
            profile.Bio);
    }

    // The request page for a slot. Null when the user has no mother profile or the slot does not exist
    // for this topic at all; Problem is set when it exists but she cannot request it now.
    public async Task<BookingRequestPage?> GetRequestPageAsync(
        string userId,
        int slotId,
        Topic topic,
        CancellationToken cancellationToken = default)
    {
        var motherId = await GetMotherIdAsync(userId, cancellationToken);
        if (motherId is null)
        {
            return null;
        }

        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        var now = PolandTime.Now;
        var slot = (await GetCandidateSlotsAsync(topic, null, slotId, now, cancellationToken)).SingleOrDefault();
        BookingStatus? problem = null;
        if (slot is null)
        {
            // Show the time and professional even though it can no longer be requested.
            slot = await GetSlotAsync(slotId, topic, cancellationToken);
            if (slot is null)
            {
                return null;
            }

            problem = BookingStatus.SlotTaken;
        }
        else
        {
            problem = await CheckMotherAsync(motherId.Value, slot, now, cancellationToken);
        }

        var babies = IsBabyTopic(topic)
            ? await _db.Babies
                .Where(b => b.MotherProfileId == motherId)
                .OrderBy(b => b.BirthDate)
                .ThenBy(b => b.Id)
                .ToListAsync(cancellationToken)
            : [];

        return new BookingRequestPage(slot, problem, babies);
    }

    // Sends the request: all checks and the optional sharing happen in one transaction. When two mothers
    // request the same slot at the same moment, the unique index lets only one of them in; the other gets SlotTaken.
    public async Task<BookingResult> RequestAsync(
        string userId,
        BookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var concern = request.Concern.Trim();
        if (concern.Length == 0 || concern.Length > Appointment.ConcernMaxLength)
        {
            return new BookingResult(BookingStatus.Invalid);
        }

        var motherId = await GetMotherIdAsync(userId, cancellationToken);
        if (motherId is null)
        {
            return new BookingResult(BookingStatus.Invalid);
        }

        Baby? baby = null;
        if (request.ShareBabyId is { } babyId)
        {
            baby = await _db.Babies.SingleOrDefaultAsync(b => b.Id == babyId && b.MotherProfileId == motherId, cancellationToken);
            if (baby is null || !IsBabyTopic(request.Topic))
            {
                return new BookingResult(BookingStatus.Invalid);
            }
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        var now = PolandTime.Now;
        var slot = (await GetCandidateSlotsAsync(request.Topic, null, request.SlotId, now, cancellationToken)).SingleOrDefault();
        if (slot is null)
        {
            return new BookingResult(BookingStatus.SlotTaken);
        }

        if (await CheckMotherAsync(motherId.Value, slot, now, cancellationToken) is { } problem)
        {
            return new BookingResult(problem);
        }

        var utcNow = DateTime.UtcNow;
        _db.Appointments.Add(new Appointment
        {
            MotherProfileId = motherId.Value,
            AvailabilitySlotId = slot.SlotId,
            Topic = request.Topic,
            ConcernText = concern,
            Status = AppointmentStatus.Requested,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another mother's request for this slot was saved first.
            return new BookingResult(BookingStatus.SlotTaken);
        }

        // One-tap sharing sets the same shared switch the records already have.
        if (request.ShareCheckIns)
        {
            var today = PolandTime.Today;
            var from = today.AddDays(-(Appointment.ShareDays - 1));
            await _db.DailyCheckIns
                .Where(c => c.MotherProfileId == motherId && !c.IsShared && c.Date >= from && c.Date <= today)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(c => c.IsShared, true).SetProperty(c => c.UpdatedAt, utcNow),
                    cancellationToken);
        }

        if (baby is not null)
        {
            var from = utcNow.AddDays(-Appointment.ShareDays);
            await _db.SleepRecords
                .Where(r => r.BabyId == baby.Id && !r.IsShared && r.StartTime >= from)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(r => r.IsShared, true).SetProperty(r => r.UpdatedAt, utcNow),
                    cancellationToken);
            await _db.FeedingRecords
                .Where(r => r.BabyId == baby.Id && !r.IsShared && r.Time >= from)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(r => r.IsShared, true).SetProperty(r => r.UpdatedAt, utcNow),
                    cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new BookingResult(BookingStatus.Sent, slot.Professional.DisplayName, ConfirmBy(slot.Date, slot.Hour));
    }

    // The mother-side limits: at most MaxUpcoming upcoming appointments, and none already at that date and hour.
    private async Task<BookingStatus?> CheckMotherAsync(
        int motherId,
        FreeSlot slot,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var active = await GetActiveAsync(motherId, cancellationToken);
        if (active.Any(a => a.Date == slot.Date && a.Hour == slot.Hour))
        {
            return BookingStatus.SameTime;
        }

        if (active.Count(a => SlotStart(a.Date, a.Hour) > now) >= Appointment.MaxUpcoming)
        {
            return BookingStatus.TooMany;
        }

        return null;
    }

    // Her requested and confirmed appointments, as slot date and hour.
    private async Task<List<(DateOnly Date, int Hour)>> GetActiveAsync(int motherId, CancellationToken cancellationToken)
    {
        return (await _db.Appointments
                .Where(a => a.MotherProfileId == motherId && Appointment.ActiveStatuses.Contains(a.Status))
                .Join(_db.AvailabilitySlots, a => a.AvailabilitySlotId, s => s.Id, (a, s) => new { s.Date, s.StartHour })
                .ToListAsync(cancellationToken))
            .Select(s => (s.Date, s.StartHour))
            .ToList();
    }

    // Slots that can be requested now for the topic (optionally one slot, optionally a language),
    // ordered by time and professional name.
    private async Task<List<FreeSlot>> GetCandidateSlotsAsync(
        Topic topic,
        Language? language,
        int? slotId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var earliest = now + Appointment.MinNotice;
        var latest = now.AddDays(Appointment.MaxDaysAhead);
        var firstDate = DateOnly.FromDateTime(earliest);
        var lastDate = DateOnly.FromDateTime(latest);

        var query = _db.AvailabilitySlots
            .Where(s => s.IsOpen && s.Date >= firstDate && s.Date <= lastDate
                && !_db.Appointments.Any(a => a.AvailabilitySlotId == s.Id && Appointment.ActiveStatuses.Contains(a.Status))
                && _db.ProfessionalCategories.Any(c => c.ProfessionalProfileId == s.ProfessionalProfileId && c.Category == topic));
        if (slotId is not null)
        {
            query = query.Where(s => s.Id == slotId);
        }

        if (language is not null)
        {
            query = query.Where(s => _db.ProfessionalLanguages
                .Any(l => l.ProfessionalProfileId == s.ProfessionalProfileId && l.Language == language));
        }

        var rows = await query
            .Join(
                _db.ProfessionalProfiles.Where(p => p.ApprovalStatus == ApprovalStatus.Approved),
                s => s.ProfessionalProfileId,
                p => p.Id,
                (s, p) => new { s.Id, s.Date, s.StartHour, ProfessionalId = p.Id, p.DisplayName, p.ProfessionalType })
            .ToListAsync(cancellationToken);

        rows = rows
            .Where(r => SlotStart(r.Date, r.StartHour) >= earliest && SlotStart(r.Date, r.StartHour) <= latest)
            .ToList();

        var languages = await GetLanguagesAsync(rows.Select(r => r.ProfessionalId).Distinct().ToList(), cancellationToken);
        return rows
            .OrderBy(r => r.Date)
            .ThenBy(r => r.StartHour)
            .ThenBy(r => r.DisplayName)
            .Select(r => new FreeSlot(
                r.Id,
                r.Date,
                r.StartHour,
                new ProfessionalCard(r.ProfessionalId, r.DisplayName, r.ProfessionalType, languages[r.ProfessionalId])))
            .ToList();
    }

    // A slot of an approved professional who covers the topic, whatever its state; for showing it on the request page.
    private async Task<FreeSlot?> GetSlotAsync(int slotId, Topic topic, CancellationToken cancellationToken)
    {
        var row = await _db.AvailabilitySlots
            .Where(s => s.Id == slotId
                && _db.ProfessionalCategories.Any(c => c.ProfessionalProfileId == s.ProfessionalProfileId && c.Category == topic))
            .Join(
                _db.ProfessionalProfiles.Where(p => p.ApprovalStatus == ApprovalStatus.Approved),
                s => s.ProfessionalProfileId,
                p => p.Id,
                (s, p) => new { s.Id, s.Date, s.StartHour, ProfessionalId = p.Id, p.DisplayName, p.ProfessionalType })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var languages = await GetLanguagesAsync([row.ProfessionalId], cancellationToken);
        return new FreeSlot(
            row.Id,
            row.Date,
            row.StartHour,
            new ProfessionalCard(row.ProfessionalId, row.DisplayName, row.ProfessionalType, languages[row.ProfessionalId]));
    }

    private async Task<Dictionary<int, IReadOnlyList<Language>>> GetLanguagesAsync(
        List<int> profileIds,
        CancellationToken cancellationToken)
    {
        var rows = await _db.ProfessionalLanguages
            .Where(l => profileIds.Contains(l.ProfessionalProfileId))
            .OrderBy(l => l.Id)
            .Select(l => new { l.ProfessionalProfileId, l.Language })
            .ToListAsync(cancellationToken);
        return profileIds.ToDictionary(
            id => id,
            id => (IReadOnlyList<Language>)rows.Where(r => r.ProfessionalProfileId == id).Select(r => r.Language).ToList());
    }

    private async Task<int?> GetMotherIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _db.MotherProfiles
            .Where(m => m.UserId == userId)
            .Select(m => (int?)m.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
