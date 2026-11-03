namespace Nurtura.Web.Localization;

// Every time of day in the app is shown and entered on the 24-hour clock, in both languages.
// wwwroot/js/date-input.js uses the same format ("H:i" in flatpickr's notation).
public static class TimeFormats
{
    public const string Display = "HH:mm";

    // For asp-format on inputs.
    public const string DisplayPattern = "{0:" + Display + "}";
}
