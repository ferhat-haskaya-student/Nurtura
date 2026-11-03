using System.Globalization;
using Microsoft.Extensions.Localization;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Areas.Mother;

// How sleeps and feeds read on the baby page, the timer and the feeding screen. Times in Warsaw, 24-hour clock.
public static class SleepText
{
    // "7 h 13 min", "3 h", "45 min". wwwroot/js/sleep-timer.js follows the same rule.
    public static string Duration(IStringLocalizer localizer, TimeSpan duration)
    {
        var totalMinutes = Math.Max(0, (int)duration.TotalMinutes);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        if (hours == 0)
        {
            return localizer["Duration_Minutes", minutes];
        }

        return minutes == 0
            ? localizer["Duration_Hours", hours]
            : localizer["Duration_HoursMinutes", hours, minutes];
    }

    // How long a finished sleep lasted.
    public static string Duration(IStringLocalizer localizer, SleepRecord sleep) =>
        Duration(localizer, sleep.EndTime!.Value - sleep.StartTime);

    // Both tiles: "just now", "25 min ago", "1 h 24 min ago", "2 h ago", then "2 days ago" from a day.
    public static string AgoPrecise(IStringLocalizer localizer, TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes < 1)
        {
            return localizer["Ago_JustNow"];
        }

        return elapsed.TotalDays < 1
            ? localizer["Ago_Precise", Duration(localizer, elapsed)]
            : Count(localizer, "Ago_Days", (int)elapsed.TotalDays);
    }

    // "21:55 – 05:08 (+1 day)", or "21:55 – now" while it runs. A sleep belongs to the day it started.
    public static string Range(IStringLocalizer localizer, SleepRecord sleep)
    {
        var start = PolandTime.ToLocal(sleep.StartTime);
        if (sleep.EndTime is null)
        {
            return localizer["Sleep_RangeRunning", Time(start)];
        }

        var end = PolandTime.ToLocal(sleep.EndTime.Value);
        var range = localizer["Sleep_Range", Time(start), Time(end)];
        return end.Date > start.Date ? localizer["Sleep_RangeNextDay", range] : range;
    }

    // "02-11-2026, 21:55 – 05:08 (+1 day)": names the sleep a save clashes with.
    public static string WithDate(IStringLocalizer localizer, SleepRecord sleep) =>
        localizer["Sleep_WithDate", PolandTime.ToLocal(sleep.StartTime).ToString(DateFormats.Display), Range(localizer, sleep)];

    // Tile: "Bottle · 60 ml", "Breast · left", "Solids".
    // Timeline (full): "13:06 · Bottle · 60 ml · formula", "08:30 · Breast · left · 15 min", "11:00 · Solids".
    public static string FeedSummary(IStringLocalizer localizer, FeedingRecord feed, bool full)
    {
        var parts = new List<string>();
        if (full)
        {
            parts.Add(Time(PolandTime.ToLocal(feed.Time)));
        }

        parts.Add(localizer["FeedingType_" + feed.Type]);
        if (feed.BreastSide is not null)
        {
            parts.Add(localizer["BreastSide_Short_" + feed.BreastSide]);
        }

        if (feed.Minutes is not null && full)
        {
            parts.Add(localizer["Duration_Minutes", feed.Minutes]);
        }

        if (feed.AmountMl is not null)
        {
            parts.Add(localizer["Feeding_AmountMlValue", feed.AmountMl]);
        }

        if (feed.BottleContent is not null && full)
        {
            parts.Add(localizer["BottleContent_Short_" + feed.BottleContent]);
        }

        return string.Join(localizer["List_Separator"], parts);
    }

    // ":" in a format string is the culture's time separator; the invariant one is always ":".
    public static string Time(DateTime local) => local.ToString(TimeFormats.Display, CultureInfo.InvariantCulture);

    private static string Count(IStringLocalizer localizer, string baseKey, int count) =>
        localizer[Plural.Key(baseKey, count), count];
}
