using System.Globalization;
using Microsoft.Extensions.Localization;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Core.Services;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Areas.Mother;

// How the baby routine summary reads on the baby page card and the full summary page.
public static class RoutineText
{
    public const string None = "–";

    // "Mon 03-11": a routine day is named after the date it starts on, at 07:00.
    public static string DayShort(IStringLocalizer localizer, DateOnly date) =>
        localizer["Weekday_Short_" + date.DayOfWeek] + " " + DayMonth(date);

    // "Monday 03-11", for the chart's text version.
    public static string DayLong(IStringLocalizer localizer, DateOnly date) =>
        localizer["Weekday_" + date.DayOfWeek] + " " + DayMonth(date);

    public static string DayMonth(DateOnly date) => date.ToString("dd-MM", CultureInfo.InvariantCulture);

    // An average: "9", "8.6" (one decimal only when needed, in the page's culture).
    public static string Number(double value) => Math.Round(value, 1).ToString("0.#", CultureInfo.CurrentCulture);

    public static string Time(DateTime utc) => SleepText.Time(PolandTime.ToLocal(utc));

    public static string Duration(IStringLocalizer localizer, TimeSpan? value) =>
        value is null ? None : SleepText.Duration(localizer, value.Value);

    // "Based on 6 full days + today so far".
    public static string BasedOn(IStringLocalizer localizer, int fullDays) =>
        localizer[Plural.Key("Routine_BasedOn", fullDays), fullDays];

    // "3 feeds".
    public static string FeedCount(IStringLocalizer localizer, int count) =>
        localizer[Plural.Key("Routine_FeedCount", count), count];

    // "Tue 03-11 night, 19:42 – 01:15".
    public static string LongestNightNote(IStringLocalizer localizer, RoutineLongestNight night) =>
        localizer["Routine_LongestNightNote", DayShort(localizer, night.Date),
            Time(night.Sleep.StartTime), Time(night.Sleep.EndTime!.Value)];

    // Card line: "Sleep: 15 h a day · longest night 6 h 20 min". Null when no full day has a sleep.
    public static string? SleepLine(IStringLocalizer localizer, BabyRoutine routine)
    {
        if (routine.AverageSleep is null)
        {
            return null;
        }

        var line = localizer["Routine_CardSleep", SleepText.Duration(localizer, routine.AverageSleep.Value)].Value;
        return routine.LongestNight is null
            ? line
            : line + localizer["List_Separator"] + localizer["Routine_CardLongestNight", SleepText.Duration(localizer, routine.LongestNight.Duration)];
    }

    // Card line: "Feeds: 7 a day · about every 2 h 20 min in the day". Null when no full day has a feed.
    public static string? FeedLine(IStringLocalizer localizer, BabyRoutine routine)
    {
        if (routine.AverageFeeds is null)
        {
            return null;
        }

        var line = localizer["Routine_CardFeeds", Number(routine.AverageFeeds.Value)].Value;
        return routine.AverageDaytimeGap is null
            ? line
            : line + localizer["List_Separator"] + localizer["Routine_CardEveryDaytime", SleepText.Duration(localizer, routine.AverageDaytimeGap.Value)];
    }

    // Day card: "7 h 10 min · woke at 06:40", "–", or "– (tonight)" for today.
    public static string NightSleep(IStringLocalizer localizer, RoutineDay day)
    {
        if (day.WokeAtUtc is { } woke)
        {
            return localizer["Routine_WokeAt", SleepText.Duration(localizer, day.NightTotal), Time(woke)];
        }

        return day.IsToday ? localizer["Routine_Tonight"] : None;
    }

    // Day card: "4 · 3 h 10 min" or "–".
    public static string Naps(IStringLocalizer localizer, RoutineDay day) =>
        day.Naps.Count == 0 ? None : localizer["Routine_CountAndTotal", day.Naps.Count, SleepText.Duration(localizer, day.NapTotal)];

    // Day card: "6 breast · 1 bottle", only the types she logged.
    public static string FeedsByType(IStringLocalizer localizer, RoutineDay day)
    {
        var parts = new List<string>();
        foreach (var (type, count) in new[] { (FeedingType.Breast, day.Breast), (FeedingType.Bottle, day.Bottle), (FeedingType.Solid, day.Solid) })
        {
            if (count > 0)
            {
                parts.Add(localizer["Routine_TypeCount_" + type, count]);
            }
        }

        return parts.Count == 0 ? None : string.Join(localizer["List_Separator"], parts);
    }

    // Day card: "1 (01:10)", "0", or "– (tonight)" for today before any.
    public static string NightFeeds(IStringLocalizer localizer, RoutineDay day)
    {
        var feeds = day.NightFeeds;
        if (feeds.Count == 0)
        {
            return day.IsToday ? localizer["Routine_Tonight"] : "0";
        }

        return localizer["Routine_CountAndTimes", feeds.Count, string.Join(", ", feeds.Select(f => Time(f.Time)))];
    }

    // The chart's text version of one day: "naps 09:10 – 10:00; night 19:30 – 06:10; feeds 07:05, 09:40".
    public static string DayAsText(IStringLocalizer localizer, RoutineDay day)
    {
        string Ranges(IEnumerable<SleepRecord> sleeps) => string.Join(", ", sleeps.Select(s =>
            localizer["Sleep_Range", Time(s.StartTime), Time(s.EndTime!.Value)].Value));

        var parts = new List<string>();
        if (day.Naps.Count > 0)
        {
            parts.Add(localizer["Routine_TextNaps", Ranges(day.Naps)]);
        }

        if (day.Nights.Count > 0)
        {
            parts.Add(localizer["Routine_TextNight", Ranges(day.Nights)]);
        }

        if (day.HasFeeds)
        {
            parts.Add(localizer["Routine_TextFeeds", string.Join(", ", day.Feeds.Select(f => Time(f.Time)))]);
        }

        return parts.Count == 0 ? localizer["Routine_TextNothing"] : string.Join("; ", parts);
    }
}
