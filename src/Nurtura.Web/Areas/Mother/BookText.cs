using System.Globalization;
using Microsoft.Extensions.Localization;
using Nurtura.Core.Entities;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Areas.Mother;

// How slot times and professionals read on the booking pages. All times are Poland time.
public static class BookText
{
    // "Thursday 06-11-2026".
    public static string Day(IStringLocalizer localizer, DateOnly date) =>
        localizer["Weekday_" + date.DayOfWeek] + " " + date.ToString(DateFormats.Display, CultureInfo.InvariantCulture);

    // "14:00".
    public static string Hour(int hour) => new TimeOnly(hour, 0).ToString(TimeFormats.Display, CultureInfo.InvariantCulture);

    // "14:00–15:00": every session is one hour.
    public static string HourRange(int hour) => Hour(hour) + "–" + Hour(hour + 1);

    // "Thursday 06-11-2026, 12:00"; the order comes from the language ("06-11-2026 (Czwartek), 12:00"
    // in Polish, where "do" + weekday would need another grammatical case).
    public static string Moment(IStringLocalizer localizer, DateTime polandTime) =>
        localizer[
            "Book_Moment",
            localizer["Weekday_" + polandTime.DayOfWeek],
            polandTime.ToString(DateFormats.Display, CultureInfo.InvariantCulture),
            polandTime.ToString(TimeFormats.Display, CultureInfo.InvariantCulture)];

    // "Polish, English".
    public static string Languages(IStringLocalizer localizer, IEnumerable<Language> languages) =>
        string.Join(", ", languages.Select(l => localizer["Language_" + l].Value));
}
