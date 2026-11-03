using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// One entry on the baby page's "Last 7 days" list: a finished sleep (at its start time) or a feed.
public sealed record TimelineEntry(DateTime TimeUtc, SleepRecord? Sleep, FeedingRecord? Feed);

public sealed record TimelineDay(DateOnly Date, IReadOnlyList<TimelineEntry> Entries);

// The baby page's one list of sleeps and feeds, newest first, grouped by the Warsaw day.
public static class BabyTimeline
{
    // Today and the days before it, this many days in all.
    public const int Days = 7;

    // Warsaw midnight at the start of the first day. Midnight always exists in Warsaw; the clocks change at night but not at 00:00.
    public static DateTime SinceUtc()
    {
        PolandTime.TryToUtc(PolandTime.Today.AddDays(1 - Days), TimeOnly.MinValue, out var since);
        return since;
    }

    public static IReadOnlyList<TimelineDay> Build(IEnumerable<SleepRecord> sleeps, IEnumerable<FeedingRecord> feeds)
    {
        return sleeps.Select(s => new TimelineEntry(s.StartTime, s, null))
            .Concat(feeds.Select(f => new TimelineEntry(f.Time, null, f)))
            .OrderByDescending(e => e.TimeUtc)
            .GroupBy(e => DateOnly.FromDateTime(PolandTime.ToLocal(e.TimeUtc)))
            .Select(g => new TimelineDay(g.Key, g.ToList()))
            .ToList();
    }
}
