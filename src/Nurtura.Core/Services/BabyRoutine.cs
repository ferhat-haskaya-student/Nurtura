using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// One routine day, from 07:00 to 07:00 the next morning (Warsaw time), so a night stays in one piece.
// Each sleep and feed belongs whole to the routine day it started in. Sleeps are finished ones only.
public sealed record RoutineDay(
    DateOnly Date,
    DateTime StartUtc,
    DateTime EndUtc,
    bool IsToday,
    IReadOnlyList<SleepRecord> Naps,
    IReadOnlyList<SleepRecord> Nights,
    IReadOnlyList<FeedingRecord> Feeds)
{
    public TimeSpan NapTotal => BabyRoutine.Total(Naps);

    public TimeSpan NightTotal => BabyRoutine.Total(Nights);

    public TimeSpan SleepTotal => NapTotal + NightTotal;

    public bool HasSleep => Naps.Count + Nights.Count > 0;

    public bool HasFeeds => Feeds.Count > 0;

    // When the night ended: the end of the day's last night sleep.
    public DateTime? WokeAtUtc => Nights.Count == 0 ? null : Nights.Max(s => s.EndTime!.Value);

    public int Breast => Feeds.Count(f => f.Type == FeedingType.Breast);

    public int Bottle => Feeds.Count(f => f.Type == FeedingType.Bottle);

    public int Solid => Feeds.Count(f => f.Type == FeedingType.Solid);

    // Only from feeds with an amount; null when none has one.
    public int? BottleMl => Feeds.Any(f => f.AmountMl is not null) ? Feeds.Sum(f => f.AmountMl ?? 0) : null;

    // 22:00 to 06:00, so the bedtime feed is not one.
    public IReadOnlyList<FeedingRecord> NightFeeds => Feeds.Where(f => BabyRoutine.IsNightFeed(f.Time)).ToList();

    // Gaps between feeds following each other in the day part (07:00 to 19:00).
    public IReadOnlyList<TimeSpan> DaytimeGaps
    {
        get
        {
            var daytime = Feeds.Where(f => BabyRoutine.IsDaytime(f.Time)).ToList();
            return daytime.Zip(daytime.Skip(1)).Select(p => p.Second.Time - p.First.Time).ToList();
        }
    }
}

// The longest night sleep on a full day, and the routine day it belongs to.
public sealed record RoutineLongestNight(DateOnly Date, SleepRecord Sleep)
{
    public TimeSpan Duration => Sleep.EndTime!.Value - Sleep.StartTime;
}

// The baby's last 7 routine days of sleep and feeds, worked out on request and never stored.
// It only reports what was logged: no reference figures, nothing marked good or bad.
// Today is shown "so far" and left out of every average. Sleep averages count full days with a sleep,
// feed averages full days with a feed.
public sealed record BabyRoutine(
    IReadOnlyList<RoutineDay> Days,
    int SleepDays,
    TimeSpan? AverageSleep,
    TimeSpan? AverageNight,
    double? AverageNaps,
    TimeSpan? AverageNapTime,
    RoutineLongestNight? LongestNight,
    int FeedDays,
    double? AverageFeeds,
    TimeSpan? AverageDaytimeGap,
    double? AverageNightFeeds,
    int? AverageBottleMl)
{
    public static readonly TimeOnly DayStarts = new(7, 0);

    // A sleep starting from here until DayStarts is night sleep; earlier in the day it is a nap.
    public static readonly TimeOnly NightStarts = new(19, 0);

    public static readonly TimeOnly NightFeedsFrom = new(22, 0);

    public static readonly TimeOnly NightFeedsUntil = new(6, 0);

    public RoutineDay Today => Days[0];

    public bool HasFullDay => SleepDays > 0 || FeedDays > 0;

    public bool HasAny => Days.Any(d => d.HasSleep || d.HasFeeds);

    public bool HasSleep => Days.Any(d => d.HasSleep);

    public int Breast => Days.Sum(d => d.Breast);

    public int Bottle => Days.Sum(d => d.Bottle);

    public int Solid => Days.Sum(d => d.Solid);

    public int FeedCount => Days.Sum(d => d.Feeds.Count);

    // The routine day a moment belongs to: before 07:00 it is still the previous day's night.
    public static DateOnly RoutineDate(DateTime utc)
    {
        var local = PolandTime.ToLocal(utc);
        var date = DateOnly.FromDateTime(local);
        return TimeOnly.FromDateTime(local) < DayStarts ? date.AddDays(-1) : date;
    }

    // 07:00 always exists in Warsaw; the clocks change at night.
    public static DateTime DayStartUtc(DateOnly date)
    {
        PolandTime.TryToUtc(date, DayStarts, out var utc);
        return utc;
    }

    // Start of the oldest routine day shown.
    public static DateTime SinceUtc(DateTime nowUtc) => DayStartUtc(RoutineDate(nowUtc).AddDays(1 - BabyTimeline.Days));

    public static bool IsNap(DateTime startUtc)
    {
        var time = TimeOnly.FromDateTime(PolandTime.ToLocal(startUtc));
        return time >= DayStarts && time < NightStarts;
    }

    public static bool IsDaytime(DateTime utc) => IsNap(utc);

    public static bool IsNightFeed(DateTime utc)
    {
        var time = TimeOnly.FromDateTime(PolandTime.ToLocal(utc));
        return time >= NightFeedsFrom || time < NightFeedsUntil;
    }

    public static TimeSpan Total(IEnumerable<SleepRecord> sleeps) =>
        sleeps.Aggregate(TimeSpan.Zero, (sum, s) => sum + (s.EndTime!.Value - s.StartTime));

    // Running sleeps are left out. Days come back newest first, today first, always BabyTimeline.Days of them.
    public static BabyRoutine Build(IEnumerable<SleepRecord> sleeps, IEnumerable<FeedingRecord> feeds, DateTime nowUtc)
    {
        var today = RoutineDate(nowUtc);
        var finished = sleeps.Where(s => s.EndTime is not null).OrderBy(s => s.StartTime).ToList();
        var feedList = feeds.OrderBy(f => f.Time).ThenBy(f => f.Id).ToList();

        var days = new List<RoutineDay>();
        for (var i = 0; i < BabyTimeline.Days; i++)
        {
            var date = today.AddDays(-i);
            var (start, end) = (DayStartUtc(date), DayStartUtc(date.AddDays(1)));
            var daySleeps = finished.Where(s => s.StartTime >= start && s.StartTime < end).ToList();
            days.Add(new RoutineDay(
                date,
                start,
                end,
                i == 0,
                daySleeps.Where(s => IsNap(s.StartTime)).ToList(),
                daySleeps.Where(s => !IsNap(s.StartTime)).ToList(),
                feedList.Where(f => f.Time >= start && f.Time < end).ToList()));
        }

        var full = days.Where(d => !d.IsToday).ToList();
        var sleepDays = full.Where(d => d.HasSleep).ToList();
        var feedDays = full.Where(d => d.HasFeeds).ToList();
        var mlDays = full.Where(d => d.BottleMl is not null).ToList();
        var gaps = full.SelectMany(d => d.DaytimeGaps).ToList();

        var longest = full
            .SelectMany(d => d.Nights.Select(s => new RoutineLongestNight(d.Date, s)))
            .OrderByDescending(n => n.Duration)
            .FirstOrDefault();

        return new BabyRoutine(
            days,
            sleepDays.Count,
            Average(sleepDays.Select(d => d.SleepTotal)),
            Average(sleepDays.Select(d => d.NightTotal)),
            sleepDays.Count == 0 ? null : sleepDays.Average(d => d.Naps.Count),
            Average(sleepDays.Select(d => d.NapTotal)),
            longest,
            feedDays.Count,
            feedDays.Count == 0 ? null : feedDays.Average(d => d.Feeds.Count),
            Average(gaps),
            feedDays.Count == 0 ? null : feedDays.Average(d => d.NightFeeds.Count),
            mlDays.Count == 0 ? null : (int)Math.Round(mlDays.Average(d => d.BottleMl!.Value), MidpointRounding.AwayFromZero));
    }

    // To the whole minute; null when there is nothing to average.
    private static TimeSpan? Average(IEnumerable<TimeSpan> values)
    {
        var list = values.ToList();
        return list.Count == 0
            ? null
            : TimeSpan.FromMinutes(Math.Round(list.Average(v => v.TotalMinutes), MidpointRounding.AwayFromZero));
    }
}
