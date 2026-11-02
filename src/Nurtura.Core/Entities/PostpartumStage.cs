namespace Nurtura.Core.Entities;

// Her postpartum stage, calculated from her youngest baby's birth date. Never stored.
// Each band includes its lower bound.
public enum PostpartumStage
{
    FirstSixWeeks,
    SixWeeksToThreeMonths,
    ThreeToSixMonths,
    SixToTwelveMonths,
    OverOneYear
}
