using Nurtura.Core.Services;

namespace Nurtura.Tests.Rules;

// Baby age is never stored, always calculated from the birth date (pure rule, no database). Under 1 month it is
// weeks and days, under 1 year months and weeks, then years and months. Months are full calendar months.
public sealed class BabyAgeTests
{
    private static BabyAge Age(string birthDate, string today) =>
        BabyAge.Calculate(DateOnly.Parse(birthDate), DateOnly.Parse(today));

    [Theory]
    [InlineData("2026-01-01", "2026-01-01", 0, 0, 0, 0)]
    [InlineData("2026-01-01", "2026-01-07", 0, 0, 0, 6)]
    [InlineData("2026-01-01", "2026-01-08", 0, 0, 1, 0)]
    [InlineData("2026-01-01", "2026-01-31", 0, 0, 4, 2)]
    [InlineData("2026-01-01", "2026-02-01", 0, 1, 0, 0)]
    [InlineData("2026-01-01", "2026-04-16", 0, 3, 2, 0)]
    [InlineData("2026-01-01", "2026-12-31", 0, 11, 4, 0)]
    [InlineData("2026-01-01", "2027-01-01", 1, 0, 0, 0)]
    [InlineData("2026-01-01", "2027-03-15", 1, 2, 0, 0)]
    public void Calculate_ReducesToTwoLargestUnits(string birthDate, string today, int years, int months, int weeks, int days)
    {
        Assert.Equal(new BabyAge(years, months, weeks, days), Age(birthDate, today));
    }

    [Fact]
    public void Calculate_BornOn31st_OneMonthOnLastDayOfShorterMonth()
    {
        Assert.Equal(new BabyAge(0, 0, 3, 6), Age("2026-01-31", "2026-02-27"));
        Assert.Equal(new BabyAge(0, 1, 0, 0), Age("2026-01-31", "2026-02-28"));
    }

    [Fact]
    public void Calculate_BornOnLeapDay_OneYearOn28February()
    {
        Assert.Equal(new BabyAge(0, 11, 4, 0), Age("2024-02-29", "2025-02-27"));
        Assert.Equal(new BabyAge(1, 0, 0, 0), Age("2024-02-29", "2025-02-28"));
    }

    [Fact]
    public void Calculate_TodayBeforeBirthDate_Zero()
    {
        Assert.Equal(new BabyAge(0, 0, 0, 0), Age("2026-05-10", "2026-05-09"));
    }
}
