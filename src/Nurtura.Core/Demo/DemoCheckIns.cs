using Nurtura.Core.Entities;

namespace Nurtura.Core.Demo;

// How one mother checks in: how often, her offsets from the shared recovery curve, how often she writes a note,
// and her own feelings.
internal sealed record CheckInStyle(
    double Chance,
    double Mood,
    double Sleep,
    double Energy,
    double Stress,
    double NoteChance,
    bool OftenLonely,
    double SupportedChance,
    string[] OwnNotes,
    bool AlwaysToday);

// Daily check-ins: early weeks lower sleep quality and energy and more stress, slowly better, with bad days
// (always the day after one of the baby's bad nights). Feeling tags follow the numbers. One per date.
internal static class DemoCheckIns
{
    // The fixed feeling tags' ids, as the migration inserts them.
    private const int Tired = 1;
    private const int Anxious = 2;
    private const int Overwhelmed = 3;
    private const int Sad = 4;
    private const int Lonely = 5;
    private const int Calm = 6;
    private const int Happy = 7;
    private const int Supported = 8;

    // Most of the change comes in the first 16 weeks after birth.
    private const double RecoveryDays = 112;

    public static void Generate(
        DemoMother mother, CheckInStyle style, DateOnly first, DateTime nowUtc, ISet<DateOnly> badNights, Random rng)
    {
        var now = PolandTime.ToLocal(nowUtc);
        var today = DateOnly.FromDateTime(now);
        var birth = mother.Baby.BirthDate;
        var low = new NotePool(DemoTexts.CheckInLow, rng);
        var mid = new NotePool(DemoTexts.CheckInMid, rng);
        var high = new NotePool(DemoTexts.CheckInHigh, rng);
        var badNight = new NotePool(DemoTexts.CheckInBadNight, rng);
        var own = style.OwnNotes.Length > 0 ? new NotePool(style.OwnNotes, rng) : null;

        for (var date = first; date <= today; date = date.AddDays(1))
        {
            // Today only once the morning has started.
            if (date == today && now.Hour < 7)
            {
                break;
            }

            var forced = date == today && style.AlwaysToday;
            if (!forced && !rng.Chance(style.Chance))
            {
                continue;
            }

            var recovery = Math.Clamp((date.DayNumber - birth.DayNumber) / RecoveryDays, 0, 1);
            var dayFeel = rng.Between(-0.6, 0.6);
            var afterBadNight = badNights.Contains(date.AddDays(-1));
            var badDay = afterBadNight || rng.Chance(0.06);

            var mood = 2.5 + 1.4 * recovery + style.Mood + dayFeel + Noise(rng);
            var sleep = 1.8 + 1.6 * recovery + style.Sleep + Noise(rng);
            var energy = 2.0 + 1.5 * recovery + style.Energy + dayFeel * 0.7 + Noise(rng);
            var stress = 3.8 - 1.4 * recovery + style.Stress - dayFeel * 0.8 + Noise(rng);
            if (badDay)
            {
                sleep -= 1.5;
                energy -= 1.2;
                mood -= 0.8;
                stress += 0.8;
            }

            var checkIn = new DailyCheckIn
            {
                Date = date,
                Mood = Rating(mood),
                SleepQuality = Rating(sleep),
                Energy = Rating(energy),
                Stress = Rating(stress)
            };

            checkIn.FeelingTags = Tags(checkIn, style, rng)
                .Select(id => new CheckInFeelingTag { FeelingTagId = id })
                .ToList();

            if (rng.Chance(style.NoteChance))
            {
                checkIn.Note = afterBadNight && rng.Chance(0.6) ? badNight.Next()
                    : own is not null && rng.Chance(0.25) ? own.Next()
                    : checkIn.Mood <= 2 ? low.Next()
                    : checkIn.Mood == 3 ? mid.Next()
                    : high.Next();
            }

            // Filled in in the evening; today's a little before now if the evening has not come yet.
            var created = DemoTime.Utc(date, rng.Pick(20 * 60, 22 * 60 + 30));
            if (created > nowUtc)
            {
                created = nowUtc.AddMinutes(-rng.Pick(5, 30));
            }

            checkIn.CreatedAt = created;
            checkIn.UpdatedAt = created;
            mother.CheckIns.Add(checkIn);
        }
    }

    private static IEnumerable<int> Tags(DailyCheckIn c, CheckInStyle style, Random rng)
    {
        if ((c.Energy <= 2 || c.SleepQuality <= 2) && rng.Chance(0.85) || c.Energy == 3 && rng.Chance(0.2))
        {
            yield return Tired;
        }

        if (c.Stress >= 4 && rng.Chance(0.5))
        {
            yield return Anxious;
        }

        if (c.Stress >= 4 && c.Mood <= 2 && rng.Chance(0.7) || c.Stress == 5 && rng.Chance(0.4))
        {
            yield return Overwhelmed;
        }

        if (c.Mood <= 2 && rng.Chance(0.4))
        {
            yield return Sad;
        }

        if (style.OftenLonely ? c.Mood <= 3 && rng.Chance(0.35) : c.Mood <= 2 && rng.Chance(0.1))
        {
            yield return Lonely;
        }

        if (c.Stress <= 2 && rng.Chance(0.6))
        {
            yield return Calm;
        }

        if (c.Mood >= 4 && rng.Chance(0.6))
        {
            yield return Happy;
        }

        if (c.Mood >= 3 && rng.Chance(style.SupportedChance))
        {
            yield return Supported;
        }
    }

    private static int Rating(double value) =>
        Math.Clamp((int)Math.Round(value), DailyCheckIn.RatingMin, DailyCheckIn.RatingMax);

    // Small day-to-day wobble, mostly within ±1.
    private static double Noise(Random rng) => (rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 1.5) * 0.8;
}
