namespace Nurtura.Core;

// The app is used in Poland only, so "today" is the date in Warsaw, not on the server's clock.
public static class PolandTime
{
    private const string TimeZoneId = "Europe/Warsaw";

    public static DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, TimeZoneId));
}
