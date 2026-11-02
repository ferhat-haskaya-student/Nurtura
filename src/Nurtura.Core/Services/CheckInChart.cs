namespace Nurtura.Core.Services;

// Her ratings for the last DailyCheckInService.ChartDays days, one entry per day, oldest first, ending today.
// A day without a check-in has null ratings; nothing is filled in.
public sealed record CheckInChart(IReadOnlyList<CheckInChartDay> Days, int CheckInCount)
{
    public bool HasCheckInToday => Days[^1].Mood is not null;
}

public sealed record CheckInChartDay(DateOnly Date, int? Mood, int? SleepQuality, int? Energy, int? Stress);
