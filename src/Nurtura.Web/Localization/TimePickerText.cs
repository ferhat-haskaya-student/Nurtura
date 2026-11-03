using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Nurtura.Web.Localization;

// The time picker field's text: "Today, 13:06", "Yesterday, 21:55", "Sun 01-11, 08:30".
// wwwroot/js/time-picker.js builds the same text in the browser after a change.
public static class TimePickerText
{
    // Day and month only; the picker never reaches further back than a week or so.
    public const string ShortDate = "dd-MM";

    public static string Label(IStringLocalizer localizer, DateOnly? date, TimeOnly? time, DateOnly today)
    {
        if (date is null || time is null)
        {
            return localizer["TimePicker_NotSet"];
        }

        return localizer["TimePicker_Display", Day(localizer, date.Value, today),
            time.Value.ToString(TimeFormats.Display, CultureInfo.InvariantCulture)];
    }

    public static string Day(IStringLocalizer localizer, DateOnly date, DateOnly today)
    {
        if (date == today)
        {
            return localizer["TimePicker_Today"];
        }

        return date == today.AddDays(-1)
            ? localizer["TimePicker_Yesterday"]
            : localizer["TimePicker_Day", localizer["Weekday_Short_" + date.DayOfWeek],
                date.ToString(ShortDate, CultureInfo.InvariantCulture)];
    }
}
