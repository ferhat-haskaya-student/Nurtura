using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Builds the pre-consultation summary for the logged-in professional. Reuses the check-in and baby routine
// calculations; reads only records the mother has marked shared, only inside the 14-day window.
public class ConsultationSummaryService
{
    private readonly INurturaDbContext _db;
    private readonly DailyCheckInService _checkInService;

    public ConsultationSummaryService(INurturaDbContext db, DailyCheckInService checkInService)
    {
        _db = db;
        _checkInService = checkInService;
    }

    public static bool IsCheckInTopic(Topic topic) => !BookingService.IsBabyTopic(topic);

    // Null unless the appointment is on a slot of her own approved profile, is requested or confirmed, and its
    // session has not ended yet (end of the hour, or earlier after End session).
    public async Task<ConsultationSummary?> GetAsync(string userId, int appointmentId, CancellationToken cancellationToken = default)
    {
        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        var row = await (
                from a in _db.Appointments
                where a.Id == appointmentId && Appointment.ActiveStatuses.Contains(a.Status)
                join s in _db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
                join p in _db.ProfessionalProfiles on s.ProfessionalProfileId equals p.Id
                where p.UserId == userId && p.ApprovalStatus == ApprovalStatus.Approved && p.IsActive
                join m in _db.MotherProfiles on a.MotherProfileId equals m.Id
                select new
                {
                    a.Id, s.Date, s.StartHour, a.Topic, a.ConcernText, a.BabyId, a.SessionEndedAt,
                    MotherId = m.Id, MotherName = m.DisplayName
                })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null || ConsultationRoom.End(row.Date, row.StartHour, row.SessionEndedAt) <= PolandTime.Now)
        {
            return null;
        }

        var today = PolandTime.Today;
        var from = today.AddDays(1 - ConsultationSummary.Days);

        // One line of age context: the baby's age for baby topics, her postpartum stage for the others.
        BabyAge? babyAge = null;
        PostpartumStage? stage = null;
        if (BookingService.IsBabyTopic(row.Topic))
        {
            var birthDate = row.BabyId is null
                ? null
                : await _db.Babies
                    .Where(b => b.Id == row.BabyId && b.MotherProfileId == row.MotherId)
                    .Select(b => (DateOnly?)b.BirthDate)
                    .SingleOrDefaultAsync(cancellationToken);
            babyAge = birthDate is null ? null : BabyAge.Calculate(birthDate.Value, today);
        }
        else
        {
            var youngest = await _db.Babies
                .Where(b => b.MotherProfileId == row.MotherId)
                .OrderByDescending(b => b.BirthDate)
                .Select(b => (DateOnly?)b.BirthDate)
                .FirstOrDefaultAsync(cancellationToken);
            stage = youngest is null ? null : BabyService.StageFor(youngest.Value, today);
        }

        CheckInSummary? checkIns = null;
        BabyRoutine? routine = null;
        IReadOnlyList<TimelineEntry> babyNotes = [];
        int daysWithData;
        if (IsCheckInTopic(row.Topic))
        {
            checkIns = await GetCheckInsAsync(row.MotherId, from, today, cancellationToken);
            daysWithData = checkIns.Chart.CheckInCount;
        }
        else if (babyAge is not null)
        {
            routine = await GetRoutineAsync(row.BabyId!.Value, row.Topic, cancellationToken);
            daysWithData = routine.Days.Count(d => row.Topic == Topic.BabySleep ? d.HasSleep : d.HasFeeds);
            babyNotes = NotesOf(routine);
        }
        else
        {
            daysWithData = 0;
        }

        return new ConsultationSummary(
            row.Id, row.Date, row.StartHour, row.Topic, row.MotherName, row.ConcernText,
            babyAge, stage, from, today, daysWithData, checkIns, routine, babyNotes);
    }

    // The routine holds only the shared records of the window (sleeps or feeds, by topic); these are the ones
    // with a note, newest first. A sleep sits at its start time, as on the baby page's timeline.
    private static IReadOnlyList<TimelineEntry> NotesOf(BabyRoutine routine) =>
        routine.Days
            .SelectMany(d => d.Naps.Concat(d.Nights).Select(s => new TimelineEntry(s.StartTime, s, null))
                .Concat(d.Feeds.Select(f => new TimelineEntry(f.Time, null, f))))
            .Where(e => !string.IsNullOrWhiteSpace(e.Sleep?.Note ?? e.Feed?.Note))
            .OrderByDescending(e => e.TimeUtc)
            .ToList();

    // Her shared check-ins in the window, with the same averages, feelings and chart days as her own pages.
    private async Task<CheckInSummary> GetCheckInsAsync(int motherId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var shared = await _db.DailyCheckIns
            .AsNoTracking()
            .Include(c => c.FeelingTags)
            .Where(c => c.MotherProfileId == motherId && c.IsShared && c.Date >= from && c.Date <= to)
            .OrderBy(c => c.Date)
            .ToListAsync(cancellationToken);

        var chart = DailyCheckInService.BuildChart(
            shared.Select(c => new CheckInChartDay(c.Date, c.Mood, c.SleepQuality, c.Energy, c.Stress)).ToList(),
            from,
            ConsultationSummary.Days);

        var week2From = from.AddDays(7);
        var week1 = shared.Where(c => c.Date < week2From).ToList();
        var week2 = shared.Where(c => c.Date >= week2From).ToList();
        var tags = await _checkInService.GetFeelingTagsAsync(cancellationToken);

        return new CheckInSummary(
            chart,
            new SummaryWeek(from, week2From.AddDays(-1), week1.Count, DailyCheckInService.Average(week1)),
            new SummaryWeek(week2From, to, week2.Count, DailyCheckInService.Average(week2)),
            DailyCheckInService.TopFeelings(shared, tags),
            shared
                .Where(c => !string.IsNullOrWhiteSpace(c.Note))
                .OrderByDescending(c => c.Date)
                .Select(c => new SummaryNote(c.Date, c.Note!))
                .ToList());
    }

    // The baby's shared records over the last 14 routine days, worked out exactly like the routine page:
    // sleeps for the baby sleep topic, feeds for baby feeding.
    private async Task<BabyRoutine> GetRoutineAsync(int babyId, Topic topic, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var since = BabyRoutine.SinceUtc(now, ConsultationSummary.Days);

        var sleeps = topic == Topic.BabySleep
            ? await _db.SleepRecords
                .AsNoTracking()
                .Where(s => s.BabyId == babyId && s.IsShared && s.EndTime != null && s.StartTime >= since)
                .ToListAsync(cancellationToken)
            : [];
        var feeds = topic == Topic.BabyFeeding
            ? await _db.FeedingRecords
                .AsNoTracking()
                .Where(f => f.BabyId == babyId && f.IsShared && f.Time >= since)
                .ToListAsync(cancellationToken)
            : [];

        return BabyRoutine.Build(sleeps, feeds, now, ConsultationSummary.Days);
    }
}
