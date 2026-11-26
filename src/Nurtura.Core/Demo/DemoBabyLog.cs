using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Core.Demo;

// One routine day's shape: Poland wall-clock targets in minutes after midnight, lengths in minutes,
// and how many times the baby wakes for a feed in the night.
internal sealed record DayShape(
    int WakeFrom, int WakeTo,
    int BedFrom, int BedTo,
    int AwakeFrom, int AwakeTo,
    int NapFrom, int NapTo,
    int NightWakes);

// A feed in the timeline. AwakeUntil is when the baby next falls asleep (used to place a solid meal after it).
internal sealed record LogFeed(DateTime Time, bool Night, bool Bedtime, DateTime AwakeUntil);

internal sealed class LogDay
{
    public required DateOnly Date { get; init; }

    public List<(DateTime Start, DateTime End)> Sleeps { get; } = [];

    public List<LogFeed> Feeds { get; } = [];
}

// The demo babies' sleeps and feeds. The timeline is built in UTC: wall-clock targets (wake-up, bedtime) are
// turned into UTC moments, and every length is added in real time, so the moments only move forward, across both
// clock changes, and sleeps of one baby never overlap. Each day runs from the morning wake-up to the next one:
// a feed after every wake-up, then awake time and a nap, until bedtime; the night is split into stretches by the
// night feeds. Everything is on the whole minute.
internal static class DemoBabyLog
{
    // Mother 1's agreed daily bottle ml (F, with research; design doc §4 "Demo data"), by the baby's day of life
    // (0 = the birth day): day 1 about 100, end of week 1 about 350, end of month 1 about 550, month 2 about 700,
    // month 3 about 800. Straight lines in between.
    private static readonly (int Day, int Ml)[] KasiaCurve = [(0, 100), (6, 350), (29, 550), (59, 700), (89, 800)];

    public static double KasiaDailyMl(int day)
    {
        if (day <= KasiaCurve[0].Day)
        {
            return KasiaCurve[0].Ml;
        }

        for (var i = 1; i < KasiaCurve.Length; i++)
        {
            var (toDay, toMl) = KasiaCurve[i];
            if (day <= toDay)
            {
                var (fromDay, fromMl) = KasiaCurve[i - 1];
                return fromMl + (toMl - fromMl) * (double)(day - fromDay) / (toDay - fromDay);
            }
        }

        return KasiaCurve[^1].Ml;
    }

    public static List<LogDay> Timeline(
        DateOnly first, DateOnly last, Func<DateOnly, DayShape> shapeOf, ISet<DateOnly> badNights, Random rng)
    {
        var shapes = new Dictionary<DateOnly, DayShape>();
        var wakes = new Dictionary<DateOnly, DateTime>();
        for (var date = first; date <= last.AddDays(1); date = date.AddDays(1))
        {
            var shape = shapeOf(date);
            shapes[date] = shape;
            wakes[date] = DemoTime.Utc(date, rng.Pick(shape.WakeFrom, shape.WakeTo));
        }

        var days = new List<LogDay>();
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var shape = shapes[date];
            var day = new LogDay { Date = date };
            var bed = DemoTime.Utc(date, rng.Pick(shape.BedFrom, shape.BedTo));
            Daytime(day, wakes[date], bed, shape, rng);
            Night(day, bed, wakes[date.AddDays(1)], shape.NightWakes, badNights.Contains(date), rng);
            days.Add(day);
        }

        return days;
    }

    private static void Daytime(LogDay day, DateTime wake, DateTime bed, DayShape shape, Random rng)
    {
        var t = wake;
        while (true)
        {
            var feed = t.AddMinutes(rng.Pick(0, 10));
            var awake = rng.Pick(shape.AwakeFrom, shape.AwakeTo);
            var nap = rng.Pick(shape.NapFrom, shape.NapTo);
            var napStart = t.AddMinutes(awake);
            if (napStart.AddMinutes(nap + 20) <= bed)
            {
                day.Feeds.Add(new LogFeed(feed, false, false, napStart));
                day.Sleeps.Add((napStart, napStart.AddMinutes(nap)));
                t = napStart.AddMinutes(nap);
                continue;
            }

            // The evening: awake until bedtime, with a short catnap if that would be too long.
            var remaining = (int)(bed - t).TotalMinutes;
            if (remaining > shape.AwakeTo + 45)
            {
                var catnap = rng.Pick(20, 40);
                var start = t.AddMinutes(Math.Min(awake, remaining - 45 - catnap));
                var after = start.AddMinutes(catnap);
                day.Feeds.Add(new LogFeed(feed, false, false, start));
                day.Sleeps.Add((start, after));
                if (bed - after >= TimeSpan.FromMinutes(60))
                {
                    day.Feeds.Add(new LogFeed(bed.AddMinutes(-rng.Pick(10, 25)), false, true, bed));
                }
            }
            else if (remaining >= 100)
            {
                day.Feeds.Add(new LogFeed(feed, false, false, bed));
                day.Feeds.Add(new LogFeed(bed.AddMinutes(-rng.Pick(10, 25)), false, true, bed));
            }
            else
            {
                // Woke up close to bedtime: this feed is the bedtime feed.
                day.Feeds.Add(new LogFeed(feed, false, true, bed));
            }

            return;
        }
    }

    private static void Night(LogDay day, DateTime bed, DateTime nextWake, int wakes, bool bad, Random rng)
    {
        // A bad night: one or two extra wake-ups, and one long awake spell.
        var count = bad ? wakes + rng.Pick(1, 2) : wakes;
        var awakes = Enumerable.Range(0, count).Select(_ => rng.Pick(15, 35)).ToArray();
        if (bad)
        {
            awakes[rng.Next(count)] = rng.Pick(45, 90);
        }

        var asleep = (int)(nextWake - bed).TotalMinutes - awakes.Sum();
        var weights = Enumerable.Range(0, count + 1).Select(_ => rng.Between(0.75, 1.25)).ToArray();
        var stretches = weights.Select(w => (int)(asleep * w / weights.Sum())).ToArray();
        stretches[count] = asleep - stretches[..count].Sum();

        var t = bed;
        for (var i = 0; i <= count; i++)
        {
            var end = t.AddMinutes(stretches[i]);
            day.Sleeps.Add((t, end));
            t = end;
            if (i < count)
            {
                var asleepAgain = t.AddMinutes(awakes[i]);
                day.Feeds.Add(new LogFeed(t.AddMinutes(rng.Pick(0, 5)), true, false, asleepAgain));
                t = asleepAgain;
            }
        }
    }

    // Mother 1, Zosia, from her birth day: bottles of expressed milk with about 20% formula (by volume, mostly the
    // night or bedtime bottle). Wakes every 2 to 3 hours at first, then about every 3; in month 2 longer stretches
    // with 2 night feeds; in month 3 F's routine, night 19:00 to 07:00 with one feed around 01:00 and a 2 to 3 hour
    // cycle of nap, feed and play in the day. Wake-up is never before 07:00 and month 3 bedtime never before 19:00,
    // so the routine page sorts the night and the naps cleanly.
    public static void Kasia(DemoMother mother, DateOnly last, DateTime nowUtc, ISet<DateOnly> badNights, Random rng)
    {
        var birth = mother.Baby.BirthDate;
        var timeline = Timeline(birth, last, date => KasiaShape(date.DayNumber - birth.DayNumber, rng), badNights, rng);

        // 2 or 3 days with nothing logged, never in the last 9 days.
        var missed = rng.Sample(Days(birth.AddDays(3), last.AddDays(-9)), rng.Pick(2, 3));
        var sleepNotes = new NotePool(DemoTexts.SleepNotes, rng);
        var feedNotes = new NotePool(DemoTexts.BottleNotes, rng);

        foreach (var day in timeline.Where(d => !missed.Contains(d.Date)))
        {
            var feeds = day.Feeds.OrderBy(f => f.Time).ToList();
            var target = KasiaDailyMl(day.Date.DayNumber - birth.DayNumber) * rng.Between(0.92, 1.08);
            var weights = feeds.Select(_ => rng.Between(0.85, 1.15)).ToArray();
            var amounts = weights.Select(w => Math.Max(5, RoundTo5(target * w / weights.Sum()))).ToArray();

            // Formula: the night feeds first, then the bedtime bottle, until about a fifth of the day's ml.
            var formulaTarget = amounts.Sum() * 0.2 * rng.Between(0.9, 1.1);
            var formula = new HashSet<int>();
            var formulaMl = 0;
            foreach (var i in Enumerable.Range(0, feeds.Count)
                         .OrderBy(i => feeds[i].Night ? 0 : feeds[i].Bedtime ? 1 : 2)
                         .ThenBy(i => feeds[i].Time))
            {
                if (formulaMl + amounts[i] <= formulaTarget + amounts[i] / 2.0)
                {
                    formula.Add(i);
                    formulaMl += amounts[i];
                }
            }

            var records = new List<FeedingRecord>();
            for (var i = 0; i < feeds.Count; i++)
            {
                records.Add(Feed(feeds[i].Time, nowUtc, rng, FeedingType.Bottle, amountMl: amounts[i],
                    content: formula.Contains(i) ? BottleContent.Formula : BottleContent.BreastMilk,
                    note: rng.Chance(0.03) ? feedNotes.Next() : null));
            }

            var sleeps = day.Sleeps
                .Select(s => Sleep(s.Start, s.End, nowUtc, rng, rng.Chance(0.03) ? sleepNotes.Next() : null))
                .ToList();

            DropOneSometimes(sleeps, records, day.Date, last, rng);
            mother.Sleeps.AddRange(sleeps);
            mother.Feeds.AddRange(records);
        }

        Truncate(mother, nowUtc);
    }

    private static DayShape KasiaShape(int age, Random rng)
    {
        if (age < 21)
        {
            return new DayShape(420, 480, 1290, 1380, 40, 75, 60, 120, rng.Pick(3, 4));
        }

        if (age < 30)
        {
            return new DayShape(420, 465, 1260, 1320, 50, 80, 60, 110, rng.Pick(2, 3));
        }

        if (age < 60)
        {
            // Bedtime moves from about 20:45 to about 19:45 over month 2.
            var bed = 1245 - 60 * (age - 30) / 30;
            return new DayShape(420, 450, bed - 15, bed + 15, 60, 90, 45, 110, 2);
        }

        return new DayShape(420, 440, 1140, 1160, 75, 105, 45, 90, 1);
    }

    // Mother 2, Antek, about 4 to 7 months: breastfed (side and minutes), fewer night feeds as he grows, solids
    // from a few days after he turns 6 months (WHO: from about 6 months), once a day, twice in the last 2 weeks.
    public static void Marta(DemoMother mother, DateOnly first, DateOnly last, DateTime nowUtc, ISet<DateOnly> badNights,
        Random rng)
    {
        var birth = mother.Baby.BirthDate;
        var timeline = Timeline(first, last, date => MartaShape(date.DayNumber - birth.DayNumber, rng), badNights, rng);

        // About 85% of days logged.
        var missed = rng.Sample(Days(first.AddDays(1), last.AddDays(-3)), (int)Math.Round(timeline.Count * 0.15));
        var solidsFrom = birth.AddMonths(6).AddDays(rng.Pick(3, 7));
        var breakfastFrom = last.AddDays(-13);
        var solids = new NotePool(DemoTexts.Solids, rng);
        var sleepNotes = new NotePool(DemoTexts.SleepNotes, rng);
        var feedNotes = new NotePool(DemoTexts.BreastNotes, rng);
        var side = BreastSide.Left;

        foreach (var day in timeline.Where(d => !missed.Contains(d.Date)))
        {
            var records = new List<FeedingRecord>();
            foreach (var feed in day.Feeds.OrderBy(f => f.Time))
            {
                side = side == BreastSide.Left ? BreastSide.Right : BreastSide.Left;
                int? minutes = !rng.Chance(0.8) ? null
                    : feed.Night ? rng.Pick(5, 15)
                    : rng.Chance(0.7) ? rng.PickOne([10, 10, 15, 15, 20, 20, 5, 30]) : rng.Pick(7, 25);
                records.Add(Feed(feed.Time, nowUtc, rng, FeedingType.Breast,
                    side: rng.Chance(0.15) ? BreastSide.Both : side, minutes: minutes,
                    note: rng.Chance(0.03) ? feedNotes.Next() : null));
            }

            if (day.Date >= solidsFrom)
            {
                var daytime = day.Feeds.Where(f => !f.Night && f.AwakeUntil - f.Time >= TimeSpan.FromMinutes(50))
                    .OrderBy(f => f.Time).ToList();
                var lunch = daytime.FirstOrDefault(f => DemoTime.LocalHour(f.Time) is >= 11 and < 14);
                var breakfast = day.Date >= breakfastFrom ? daytime.FirstOrDefault() : null;
                foreach (var meal in new[] { breakfast, lunch }.OfType<LogFeed>().Distinct())
                {
                    records.Add(Feed(meal.Time.AddMinutes(rng.Pick(20, 35)), nowUtc, rng, FeedingType.Solid,
                        note: solids.Next()));
                }
            }

            var sleeps = day.Sleeps
                .Select(s => Sleep(s.Start, s.End, nowUtc, rng, rng.Chance(0.03) ? sleepNotes.Next() : null))
                .ToList();

            DropOneSometimes(sleeps, records, day.Date, last, rng);
            mother.Sleeps.AddRange(sleeps);
            mother.Feeds.AddRange(records);
        }

        Truncate(mother, nowUtc);
    }

    private static DayShape MartaShape(int age, Random rng)
    {
        if (age < 150)
        {
            return new DayShape(390, 450, 1170, 1230, 90, 125, 35, 90, rng.Chance(0.8) ? 2 : 1);
        }

        if (age < 185)
        {
            return new DayShape(390, 450, 1170, 1230, 105, 140, 40, 90, rng.Chance(0.75) ? 1 : 2);
        }

        return new DayShape(390, 450, 1170, 1230, 120, 155, 45, 90, rng.Chance(0.5) ? 1 : 0);
    }

    // Mother 3, Lena, about 1 to 4 months: breastfed in the day, formula bottles at night and at bedtime (plus one
    // in the day on some days), bottles growing from about 90 to about 180 ml. Logs on about half the days, in
    // streaks; some logged days have feeds only.
    public static void Ola(DemoMother mother, DateOnly first, DateOnly last, DateTime nowUtc, ISet<DateOnly> badNights,
        Random rng)
    {
        var birth = mother.Baby.BirthDate;
        var timeline = Timeline(first, last, date => OlaShape(date.DayNumber - birth.DayNumber, rng), badNights, rng);
        var logged = HalfInStreaks(timeline.Select(d => d.Date).ToList(), rng);
        var feedNotes = new NotePool(DemoTexts.BreastNotes, rng);

        foreach (var day in timeline.Where(d => logged.Contains(d.Date)))
        {
            var age = day.Date.DayNumber - birth.DayNumber;
            var bottle = Math.Clamp(90 + 90 * (age - 30) / 92.0, 90, 180);
            var daytime = day.Feeds.Where(f => !f.Night && !f.Bedtime).ToList();
            var extraBottle = daytime.Count > 0 && rng.Chance(0.5) ? daytime[rng.Next(daytime.Count)] : null;

            var records = new List<FeedingRecord>();
            foreach (var feed in day.Feeds.OrderBy(f => f.Time))
            {
                records.Add(feed.Night || feed.Bedtime || feed == extraBottle
                    ? Feed(feed.Time, nowUtc, rng, FeedingType.Bottle,
                        amountMl: Math.Clamp(RoundTo5(bottle + rng.Pick(-10, 10)), 5, FeedingRecord.AmountMlMax),
                        content: BottleContent.Formula)
                    : Feed(feed.Time, nowUtc, rng, FeedingType.Breast,
                        side: rng.PickOne([BreastSide.Left, BreastSide.Right, BreastSide.Both]),
                        minutes: rng.Chance(0.7) ? rng.Pick(8, 25) : null,
                        note: rng.Chance(0.03) ? feedNotes.Next() : null));
            }

            // Some days she logs only the feeds.
            List<SleepRecord> sleeps = rng.Chance(0.2)
                ? []
                : day.Sleeps.Select(s => Sleep(s.Start, s.End, nowUtc, rng, null)).ToList();

            mother.Sleeps.AddRange(sleeps);
            mother.Feeds.AddRange(records);
        }

        Truncate(mother, nowUtc);
    }

    private static DayShape OlaShape(int age, Random rng)
    {
        if (age < 60)
        {
            return new DayShape(420, 480, 1260, 1320, 50, 90, 40, 120, rng.Chance(0.8) ? 3 : 2);
        }

        if (age < 90)
        {
            return new DayShape(420, 480, 1230, 1290, 60, 95, 40, 110, rng.Pick(2, 3));
        }

        return new DayShape(420, 465, 1200, 1260, 70, 105, 40, 100, 2);
    }

    // About half of the dates, in streaks of 1 to 4 days on and off.
    private static HashSet<DateOnly> HalfInStreaks(List<DateOnly> dates, Random rng)
    {
        var logged = new HashSet<DateOnly>();
        var on = rng.Chance(0.5);
        var i = 0;
        while (i < dates.Count)
        {
            var length = rng.Pick(1, 4);
            for (var j = 0; j < length && i < dates.Count; j++, i++)
            {
                if (on)
                {
                    logged.Add(dates[i]);
                }
            }

            on = !on;
        }

        // Kept between 45% and 55%.
        while (logged.Count > dates.Count * 0.55)
        {
            logged.Remove(logged.ElementAt(rng.Next(logged.Count)));
        }

        while (logged.Count < dates.Count * 0.45)
        {
            var off = dates.Where(d => !logged.Contains(d)).ToList();
            logged.Add(off[rng.Next(off.Count)]);
        }

        return logged;
    }

    // Real life: on about 5% of days one feed or one nap is not logged (never in the last 2 days). Never a night
    // stretch: that is the sleep a parent remembers.
    private static void DropOneSometimes(
        List<SleepRecord> sleeps, List<FeedingRecord> feeds, DateOnly date, DateOnly last, Random rng)
    {
        if (date > last.AddDays(-2) || !rng.Chance(0.05))
        {
            return;
        }

        var naps = sleeps.Where(s => BabyRoutine.IsNap(s.StartTime)).ToList();
        if (rng.Chance(0.5) && naps.Count > 1)
        {
            sleeps.Remove(naps[rng.Next(naps.Count)]);
        }
        else if (feeds.Count > 1)
        {
            feeds.RemoveAt(rng.Next(feeds.Count));
        }
    }

    // Nothing after now and no running sleep: a sleep that has not ended by now is left out.
    private static void Truncate(DemoMother mother, DateTime nowUtc)
    {
        mother.Sleeps.RemoveAll(s => s.EndTime > nowUtc);
        mother.Feeds.RemoveAll(f => f.Time > nowUtc);
    }

    private static SleepRecord Sleep(DateTime start, DateTime end, DateTime nowUtc, Random rng, string? note)
    {
        var logged = Min(nowUtc, end.AddMinutes(rng.Pick(1, 20)));
        return new SleepRecord
        {
            StartTime = SleepService.ToMinute(start),
            EndTime = SleepService.ToMinute(end),
            Note = note,
            CreatedAt = logged,
            UpdatedAt = logged
        };
    }

    private static FeedingRecord Feed(
        DateTime time, DateTime nowUtc, Random rng, FeedingType type, BreastSide? side = null, int? minutes = null,
        int? amountMl = null, BottleContent? content = null, string? note = null)
    {
        var logged = Min(nowUtc, time.AddMinutes(rng.Pick(1, 15)));
        return new FeedingRecord
        {
            Time = SleepService.ToMinute(time),
            Type = type,
            BreastSide = side,
            Minutes = minutes,
            AmountMl = amountMl,
            BottleContent = content,
            Note = note,
            CreatedAt = logged,
            UpdatedAt = logged
        };
    }

    private static int RoundTo5(double ml) => (int)Math.Round(ml / 5) * 5;

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static List<DateOnly> Days(DateOnly from, DateOnly to)
    {
        var days = new List<DateOnly>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            days.Add(date);
        }

        return days;
    }
}
