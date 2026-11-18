using Nurtura.Core.Services;

namespace Nurtura.Tests.Rules;

// The daily check-in's edit window and week (pure rules, no database): she can fill in or edit today and the
// past 7 days, never a future date. A week runs Monday to Sunday.
public sealed class CheckInWindowTests
{
    // A Wednesday.
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(-7, true)]
    [InlineData(-8, false)]
    public void IsEditableDate_TodayAndPastSevenDays(int daysFromToday, bool expected)
    {
        Assert.Equal(expected, DailyCheckInService.IsEditableDate(Today.AddDays(daysFromToday), Today));
    }

    [Theory]
    [InlineData("2026-11-16")]
    [InlineData("2026-11-18")]
    [InlineData("2026-11-22")]
    public void WeekStartOf_MondayToSunday(string date)
    {
        Assert.Equal(new DateOnly(2026, 11, 16), DailyCheckInService.WeekStartOf(DateOnly.Parse(date)));
    }

    [Fact]
    public void WeekStartOf_MondayAfterSunday_StartsNewWeek()
    {
        Assert.Equal(new DateOnly(2026, 11, 23), DailyCheckInService.WeekStartOf(new DateOnly(2026, 11, 23)));
    }
}
