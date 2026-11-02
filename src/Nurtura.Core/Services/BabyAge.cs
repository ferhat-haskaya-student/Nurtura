namespace Nurtura.Core.Services;

// A baby's age as shown to the mother, already reduced to the two largest units:
// under 1 week only days, under 1 month weeks and days, under 1 year months and weeks,
// then years and months. The other parts are 0.
public sealed record BabyAge(int Years, int Months, int Weeks, int Days)
{
    public static BabyAge Calculate(DateOnly birthDate, DateOnly today)
    {
        if (today < birthDate)
        {
            return new BabyAge(0, 0, 0, 0);
        }

        // Full calendar months: born 31-01, one month old on 28-02 (or 29-02).
        var months = (today.Year - birthDate.Year) * 12 + today.Month - birthDate.Month;
        if (birthDate.AddMonths(months) > today)
        {
            months--;
        }

        if (months >= 12)
        {
            return new BabyAge(months / 12, months % 12, 0, 0);
        }

        var days = today.DayNumber - birthDate.AddMonths(months).DayNumber;
        if (months > 0)
        {
            return new BabyAge(0, months, days / 7, 0);
        }

        return new BabyAge(0, 0, days / 7, days % 7);
    }
}
