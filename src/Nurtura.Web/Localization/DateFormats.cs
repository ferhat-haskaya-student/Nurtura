namespace Nurtura.Web.Localization;

// Every date in the app is shown and entered as dd-mm-yyyy, in both languages.
// wwwroot/js/date-input.js uses the same format ("d-m-Y" in flatpickr's notation).
public static class DateFormats
{
    public const string Display = "dd-MM-yyyy";

    // For asp-format on inputs and for showing dates in views.
    public const string DisplayPattern = "{0:" + Display + "}";
}
