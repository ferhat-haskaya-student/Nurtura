namespace Nurtura.Core;

// The app is used in Poland only, so "today" is the date in Warsaw, not on the server's clock.
// Moments (like sleep times) are stored in UTC and shown and entered in Warsaw time.
public static class PolandTime
{
    private const string TimeZoneId = "Europe/Warsaw";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    // Warsaw wall-clock time now.
    public static DateTime Now => ToLocal(DateTime.UtcNow);

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    // False for a time that does not exist in Warsaw (skipped when the clocks go forward).
    // A time that happens twice (when the clocks go back) is taken as the later one, in winter time.
    public static bool TryToUtc(DateOnly date, TimeOnly time, out DateTime utc)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local))
        {
            utc = default;
            return false;
        }

        utc = TimeZoneInfo.ConvertTimeToUtc(local, Zone);
        return true;
    }
}
