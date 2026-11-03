using System.Globalization;
using Microsoft.Extensions.Localization;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Areas.Mother;

// How sleeps read on the baby page and the timer screen. Times in Warsaw, 24-hour clock.
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

    // "just now", "25 min ago", "4 hours ago", "2 days ago".
    public static string Ago(IStringLocalizer localizer, TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes < 1)
        {
            return localizer["Ago_JustNow"];
        }

        if (elapsed.TotalHours < 1)
        {
            return Count(localizer, "Ago_Minutes", (int)elapsed.TotalMinutes);
        }

        return elapsed.TotalDays < 1
            ? Count(localizer, "Ago_Hours", (int)elapsed.TotalHours)
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

    // ":" in a format string is the culture's time separator; the invariant one is always ":".
    private static string Time(DateTime local) => local.ToString(TimeFormats.Display, CultureInfo.InvariantCulture);

    private static string Count(IStringLocalizer localizer, string baseKey, int count) =>
        localizer[Plural.Key(baseKey, count), count];
}
