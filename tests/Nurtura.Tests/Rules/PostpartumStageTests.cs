using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Tests.Rules;

// The mother's postpartum stage, calculated from her youngest baby's birth date (pure rule, no database).
// Five bands: 0–6 weeks, 6 weeks–3 months, 3–6 months, 6–12 months, over 1 year. Each boundary is tested
// on its last day and its first day.
public sealed class PostpartumStageTests
{
    private static readonly DateOnly Born = new(2026, 1, 1);

    [Theory]
    [InlineData("2026-01-01", PostpartumStage.FirstSixWeeks)]
    [InlineData("2026-02-11", PostpartumStage.FirstSixWeeks)]
    [InlineData("2026-02-12", PostpartumStage.SixWeeksToThreeMonths)]
    [InlineData("2026-03-31", PostpartumStage.SixWeeksToThreeMonths)]
    [InlineData("2026-04-01", PostpartumStage.ThreeToSixMonths)]
    [InlineData("2026-06-30", PostpartumStage.ThreeToSixMonths)]
    [InlineData("2026-07-01", PostpartumStage.SixToTwelveMonths)]
    [InlineData("2026-12-31", PostpartumStage.SixToTwelveMonths)]
    [InlineData("2027-01-01", PostpartumStage.OverOneYear)]
    [InlineData("2028-06-15", PostpartumStage.OverOneYear)]
    public void StageFor_AtBoundary(string today, PostpartumStage expected)
    {
        Assert.Equal(expected, BabyService.StageFor(Born, DateOnly.Parse(today)));
    }

    [Fact]
    public void Youngest_MostRecentBirth_TwinsByLaterAddedProfile()
    {
        var older = new Baby { Id = 1, BirthDate = new DateOnly(2024, 3, 1) };
        var twinA = new Baby { Id = 2, BirthDate = new DateOnly(2026, 5, 1) };
        var twinB = new Baby { Id = 3, BirthDate = new DateOnly(2026, 5, 1) };

        Assert.Same(twinB, BabyService.Youngest([older, twinB, twinA]));
        Assert.Null(BabyService.Youngest([]));
    }
}
