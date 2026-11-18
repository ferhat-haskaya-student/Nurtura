using Nurtura.Core;

namespace Nurtura.Tests.Rules;

// Times are entered in Warsaw time and stored in UTC (pure rule, no database). In 2026 the clocks go forward on
// 29 March (02:00 → 03:00) and back on 25 October (03:00 → 02:00).
public sealed class PolandTimeTests
{
    private static DateTime Utc(int month, int day, int hour, int minute) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void TryToUtc_Winter_OneHourBehind()
    {
        Assert.True(PolandTime.TryToUtc(new DateOnly(2026, 11, 20), new TimeOnly(10, 0), out var utc));
        Assert.Equal(Utc(11, 20, 9, 0), utc);
    }

    [Fact]
    public void TryToUtc_Summer_TwoHoursBehind()
    {
        Assert.True(PolandTime.TryToUtc(new DateOnly(2026, 7, 20), new TimeOnly(10, 0), out var utc));
        Assert.Equal(Utc(7, 20, 8, 0), utc);
    }

    [Fact]
    public void TryToUtc_SkippedHourWhenClocksGoForward_Refused()
    {
        Assert.False(PolandTime.TryToUtc(new DateOnly(2026, 3, 29), new TimeOnly(2, 30), out _));
        Assert.True(PolandTime.TryToUtc(new DateOnly(2026, 3, 29), new TimeOnly(3, 0), out var utc));
        Assert.Equal(Utc(3, 29, 1, 0), utc);
    }

    [Fact]
    public void TryToUtc_RepeatedHourWhenClocksGoBack_TakenAsWinterTime()
    {
        Assert.True(PolandTime.TryToUtc(new DateOnly(2026, 10, 25), new TimeOnly(2, 30), out var utc));
        Assert.Equal(Utc(10, 25, 1, 30), utc);
    }

    [Fact]
    public void ToLocal_RoundTripsTryToUtc()
    {
        var date = new DateOnly(2026, 7, 20);
        var time = new TimeOnly(21, 55);
        Assert.True(PolandTime.TryToUtc(date, time, out var utc));

        Assert.Equal(date.ToDateTime(time), PolandTime.ToLocal(utc));
    }
}
