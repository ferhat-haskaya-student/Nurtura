using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Every method takes the logged-in user's id and works only on that user's own check-ins.
// Check-ins are found by date, never by id, so no other mother's check-in can be reached.
public class DailyCheckInService
{
    private readonly INurturaDbContext _db;

    public DailyCheckInService(INurturaDbContext db)
    {
        _db = db;
    }

    // Today (in Poland) and the EditableDays before it.
    public static bool IsEditableDate(DateOnly date, DateOnly today) =>
        date <= today && date >= today.AddDays(-DailyCheckIn.EditableDays);

    // A week runs Monday to Sunday.
    public static DateOnly WeekStartOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    // A difference under 0.5 counts as about the same.
    public static RatingTrend CompareAverages(decimal current, decimal previous) =>
        Math.Abs(current - previous) < 0.5m ? RatingTrend.AboutTheSame
        : current > previous ? RatingTrend.Higher
        : RatingTrend.Lower;

    // The week containing the date, kept between the week of her first check-in and the current week.
    public async Task<CheckInWeek> GetWeekAsync(string userId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var today = PolandTime.Today;
        var currentWeekStart = WeekStartOf(today);

        var firstDate = await OwnCheckIns(userId)
            .OrderBy(c => c.Date)
            .Select(c => (DateOnly?)c.Date)
            .FirstOrDefaultAsync(cancellationToken);
        var firstWeekStart = firstDate is null ? currentWeekStart : WeekStartOf(firstDate.Value);

        var weekStart = WeekStartOf(date);
        if (weekStart > currentWeekStart)
        {
            weekStart = currentWeekStart;
        }
        else if (weekStart < firstWeekStart)
        {
            weekStart = firstWeekStart;
        }

        var previousWeekStart = weekStart.AddDays(-7);
        var weekEnd = weekStart.AddDays(6);
        var checkIns = await OwnCheckIns(userId)
            .AsNoTracking()
            .Include(c => c.FeelingTags)
            .Where(c => c.Date >= previousWeekStart && c.Date <= weekEnd)
            .ToListAsync(cancellationToken);
        var thisWeek = checkIns.Where(c => c.Date >= weekStart).ToList();
        var previousWeek = checkIns.Where(c => c.Date < weekStart).ToList();

        var tags = await GetFeelingTagsAsync(cancellationToken);
        var days = Enumerable.Range(0, 7)
            .Select(offset => weekStart.AddDays(offset))
            .Select(day => ToDay(day, thisWeek.SingleOrDefault(c => c.Date == day), tags, today))
            .ToList();

        var averages = Average(thisWeek);
        var isCurrentWeek = weekStart == currentWeekStart;

        return new CheckInWeek(
            weekStart,
            weekStart > firstWeekStart ? previousWeekStart : null,
            weekStart < currentWeekStart ? weekStart.AddDays(7) : null,
            isCurrentWeek,
            days,
            thisWeek.Count,
            isCurrentWeek ? today.DayNumber - weekStart.DayNumber + 1 : 7,
            averages,
            averages is null ? null : Average(previousWeek),
            TopFeelings(thisWeek, tags));
    }

    // The dashboard charts cover the last 4 weeks, ending today.
    public const int ChartDays = 28;

    public async Task<CheckInChart> GetChartAsync(string userId, CancellationToken cancellationToken = default)
    {
        var today = PolandTime.Today;
        var from = today.AddDays(-(ChartDays - 1));
        var checkIns = await OwnCheckIns(userId)
            .Where(c => c.Date >= from && c.Date <= today)
            .Select(c => new CheckInChartDay(c.Date, c.Mood, c.SleepQuality, c.Energy, c.Stress))
            .ToListAsync(cancellationToken);

        var days = Enumerable.Range(0, ChartDays)
            .Select(offset => from.AddDays(offset))
            .Select(day => checkIns.SingleOrDefault(c => c.Date == day) ?? new CheckInChartDay(day, null, null, null, null))
            .ToList();
        return new CheckInChart(days, checkIns.Count);
    }

    // Null when she has no check-in for that date.
    public async Task<CheckInDay?> GetDayAsync(string userId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var checkIn = await GetAsync(userId, date, cancellationToken);
        return checkIn is null
            ? null
            : ToDay(date, checkIn, await GetFeelingTagsAsync(cancellationToken), PolandTime.Today);
    }

    // Changes only who can see her check-in for that date. Allowed on any day, even after the rest is locked.
    // Returns false when she has no check-in that day.
    public async Task<bool> SetSharedAsync(string userId, DateOnly date, bool isShared, CancellationToken cancellationToken = default)
    {
        var checkIn = await OwnCheckIns(userId).SingleOrDefaultAsync(c => c.Date == date, cancellationToken);
        if (checkIn is null)
        {
            return false;
        }

        checkIn.IsShared = isShared;
        checkIn.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<DailyCheckIn> OwnCheckIns(string userId) =>
        _db.DailyCheckIns.Where(c => _db.MotherProfiles.Any(p => p.Id == c.MotherProfileId && p.UserId == userId));

    // Only the days she checked in. Null when she has none.
    private static RatingAverages? Average(IReadOnlyCollection<DailyCheckIn> checkIns)
    {
        if (checkIns.Count == 0)
        {
            return null;
        }

        static decimal Round(double value) => Math.Round((decimal)value, 1, MidpointRounding.AwayFromZero);

        return new RatingAverages(
            Round(checkIns.Average(c => c.Mood)),
            Round(checkIns.Average(c => c.SleepQuality)),
            Round(checkIns.Average(c => c.Energy)),
            Round(checkIns.Average(c => c.Stress)));
    }

    // Her three most ticked tags with the number of days each. Tags tied with the third are kept,
    // and equal counts are sorted by tag order.
    private static IReadOnlyList<FeelingCount> TopFeelings(
        IReadOnlyCollection<DailyCheckIn> checkIns,
        IReadOnlyList<FeelingTag> tags)
    {
        var counts = checkIns
            .SelectMany(c => c.FeelingTags)
            .GroupBy(t => t.FeelingTagId)
            .Select(g => (TagId: g.Key, Days: g.Count()))
            .OrderByDescending(x => x.Days)
            .ThenBy(x => x.TagId)
            .ToList();
        if (counts.Count == 0)
        {
            return [];
        }

        var cutoff = counts[Math.Min(3, counts.Count) - 1].Days;
        return counts
            .TakeWhile(x => x.Days >= cutoff)
            .Select(x => new FeelingCount(tags.Single(t => t.Id == x.TagId), x.Days))
            .ToList();
    }

    // tags is the full list in tag order.
    private static CheckInDay ToDay(DateOnly date, DailyCheckIn? checkIn, IReadOnlyList<FeelingTag> tags, DateOnly today) =>
        new(
            date,
            checkIn,
            checkIn is null ? [] : tags.Where(t => checkIn.FeelingTags.Any(f => f.FeelingTagId == t.Id)).ToList(),
            date > today,
            IsEditableDate(date, today));

    public async Task<IReadOnlyList<FeelingTag>> GetFeelingTagsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.FeelingTags
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    // Null when she has no check-in for that date.
    public async Task<DailyCheckIn?> GetAsync(string userId, DateOnly date, CancellationToken cancellationToken = default)
    {
        return await _db.DailyCheckIns
            .AsNoTracking()
            .Include(c => c.FeelingTags)
            .Where(c => c.Date == date
                && _db.MotherProfiles.Any(p => p.Id == c.MotherProfileId && p.UserId == userId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    // Creates her check-in for the date, or updates it if that date already has one.
    // Returns false when the user has no mother profile.
    public async Task<bool> SaveAsync(string userId, CheckInAnswers answers, CancellationToken cancellationToken = default)
    {
        if (!IsEditableDate(answers.Date, PolandTime.Today))
        {
            throw new ArgumentException("The date must be today or one of the past days that can still be edited.", nameof(answers));
        }

        foreach (var rating in new[] { answers.Mood, answers.SleepQuality, answers.Energy, answers.Stress })
        {
            if (rating is < DailyCheckIn.RatingMin or > DailyCheckIn.RatingMax)
            {
                throw new ArgumentException("A rating is out of range.", nameof(answers));
            }
        }

        // Stored with "\n" line breaks, so "\r\n" counts as one character towards the limit.
        var note = string.IsNullOrWhiteSpace(answers.Note) ? null : answers.Note.ReplaceLineEndings("\n").Trim();
        if (note?.Length > DailyCheckIn.NoteMaxLength)
        {
            throw new ArgumentException("The note is too long.", nameof(answers));
        }

        var tagIds = answers.FeelingTagIds.Distinct().ToList();
        var knownTagCount = await _db.FeelingTags.CountAsync(t => tagIds.Contains(t.Id), cancellationToken);
        if (knownTagCount != tagIds.Count)
        {
            throw new ArgumentException("Unknown feeling tag.", nameof(answers));
        }

        var profileId = await _db.MotherProfiles
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (profileId is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var checkIn = await _db.DailyCheckIns
            .Include(c => c.FeelingTags)
            .SingleOrDefaultAsync(c => c.MotherProfileId == profileId && c.Date == answers.Date, cancellationToken);
        if (checkIn is null)
        {
            checkIn = new DailyCheckIn
            {
                MotherProfileId = profileId.Value,
                Date = answers.Date,
                CreatedAt = now
            };
            _db.DailyCheckIns.Add(checkIn);
        }

        checkIn.Mood = answers.Mood;
        checkIn.SleepQuality = answers.SleepQuality;
        checkIn.Energy = answers.Energy;
        checkIn.Stress = answers.Stress;
        checkIn.Note = note;
        checkIn.IsShared = answers.IsShared;
        checkIn.UpdatedAt = now;

        checkIn.FeelingTags.RemoveAll(t => !tagIds.Contains(t.FeelingTagId));
        var addedTags = tagIds
            .Where(id => checkIn.FeelingTags.All(t => t.FeelingTagId != id))
            .Select(id => new CheckInFeelingTag { FeelingTagId = id })
            .ToList();
        checkIn.FeelingTags.AddRange(addedTags);

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
