using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// A professional's own availability. Every method takes the logged-in user's id and works only on
// the slots of her own approved profile.
public class AvailabilityService
{
    private const int DaysInWeek = 7;

    private readonly INurturaDbContext _db;

    public AvailabilityService(INurturaDbContext db)
    {
        _db = db;
    }

    public static DateOnly FirstWeekStart(DateOnly today) => DailyCheckInService.WeekStartOf(today);

    public static DateOnly LastWeekStart(DateOnly today) =>
        FirstWeekStart(today).AddDays(AvailabilitySlot.WeeksAhead * DaysInWeek);

    // True when the hour has already started in Poland.
    public static bool IsPast(DateOnly date, int hour, DateTime polandNow) =>
        date.ToDateTime(new TimeOnly(hour, 0)) <= polandNow;

    // The week containing the date, kept between the current week and WeeksAhead weeks after it.
    // With copyLastWeek, every hour not yet started takes the open or closed state of the same
    // hour a week earlier; nothing is saved until she saves the week.
    // Null when the user has no approved professional profile.
    public async Task<AvailabilityWeek?> GetWeekAsync(
        string userId,
        DateOnly date,
        bool copyLastWeek,
        CancellationToken cancellationToken = default)
    {
        var profileId = await GetProfileIdAsync(userId, cancellationToken);
        if (profileId is null)
        {
            return null;
        }

        var now = PolandTime.Now;
        var today = DateOnly.FromDateTime(now);
        var firstWeekStart = FirstWeekStart(today);
        var lastWeekStart = LastWeekStart(today);

        var weekStart = DailyCheckInService.WeekStartOf(date);
        if (weekStart < firstWeekStart)
        {
            weekStart = firstWeekStart;
        }
        else if (weekStart > lastWeekStart)
        {
            weekStart = lastWeekStart;
        }

        var previousWeekStart = weekStart.AddDays(-DaysInWeek);
        var weekEnd = weekStart.AddDays(DaysInWeek - 1);
        var open = (await _db.AvailabilitySlots
                .Where(s => s.ProfessionalProfileId == profileId && s.IsOpen
                    && s.Date >= previousWeekStart && s.Date <= weekEnd)
                .Select(s => new { s.Date, s.StartHour })
                .ToListAsync(cancellationToken))
            .Select(s => (s.Date, s.StartHour))
            .ToHashSet();

        var days = Enumerable.Range(0, DaysInWeek)
            .Select(offset =>
            {
                var day = weekStart.AddDays(offset);
                var hours = Enumerable
                    .Range(AvailabilitySlot.FirstHour, AvailabilitySlot.LastHour - AvailabilitySlot.FirstHour + 1)
                    .Select(hour =>
                    {
                        var isPast = IsPast(day, hour, now);
                        var source = copyLastWeek && !isPast ? day.AddDays(-DaysInWeek) : day;
                        return new AvailabilityHour(hour, open.Contains((source, hour)), isPast);
                    })
                    .ToList();
                return new AvailabilityDay(day, hours);
            })
            .ToList();

        return new AvailabilityWeek(
            weekStart,
            weekStart > firstWeekStart ? previousWeekStart : null,
            weekStart < lastWeekStart ? weekStart.AddDays(DaysInWeek) : null,
            days,
            days.Sum(d => d.Hours.Count(h => h.IsOpen)),
            copyLastWeek);
    }

    // Saves the whole week at once: the given hours are open, every other hour of the week is closed.
    // Hours that have already started are never changed, and entries outside the week or outside
    // 08:00–17:00 are ignored. Returns the number of open hours in the week, or null when the user
    // has no approved professional profile or the week is outside the current week to WeeksAhead.
    public async Task<int?> SaveWeekAsync(
        string userId,
        DateOnly weekStart,
        IEnumerable<(DateOnly Date, int Hour)> openHours,
        CancellationToken cancellationToken = default)
    {
        var profileId = await GetProfileIdAsync(userId, cancellationToken);
        if (profileId is null)
        {
            return null;
        }

        var now = PolandTime.Now;
        var today = DateOnly.FromDateTime(now);
        if (weekStart != DailyCheckInService.WeekStartOf(weekStart)
            || weekStart < FirstWeekStart(today)
            || weekStart > LastWeekStart(today))
        {
            return null;
        }

        var weekEnd = weekStart.AddDays(DaysInWeek - 1);
        var wanted = openHours.ToHashSet();

        var slots = await _db.AvailabilitySlots
            .Where(s => s.ProfessionalProfileId == profileId && s.Date >= weekStart && s.Date <= weekEnd)
            .ToListAsync(cancellationToken);
        var existing = slots.ToDictionary(s => (s.Date, s.StartHour));

        var utcNow = DateTime.UtcNow;
        for (var day = weekStart; day <= weekEnd; day = day.AddDays(1))
        {
            for (var hour = AvailabilitySlot.FirstHour; hour <= AvailabilitySlot.LastHour; hour++)
            {
                if (IsPast(day, hour, now))
                {
                    continue;
                }

                var isOpen = wanted.Contains((day, hour));
                if (existing.TryGetValue((day, hour), out var slot))
                {
                    if (slot.IsOpen != isOpen)
                    {
                        slot.IsOpen = isOpen;
                        slot.UpdatedAt = utcNow;
                    }
                }
                else if (isOpen)
                {
                    var added = new AvailabilitySlot
                    {
                        ProfessionalProfileId = profileId.Value,
                        Date = day,
                        StartHour = hour,
                        IsOpen = true,
                        CreatedAt = utcNow,
                        UpdatedAt = utcNow
                    };
                    _db.AvailabilitySlots.Add(added);
                    slots.Add(added);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return slots.Count(s => s.IsOpen);
    }

    private async Task<int?> GetProfileIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _db.ProfessionalProfiles
            .Where(p => p.UserId == userId && p.ApprovalStatus == ApprovalStatus.Approved)
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
