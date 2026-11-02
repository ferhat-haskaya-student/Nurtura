using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// One calendar week (Monday to Sunday) of a mother's check-ins with its summary. Calculated on request, never stored.
// PreviousWeekStart and NextWeekStart are null where the arrows stop.
// PreviousAverages is set only when both this week and the week before have at least one check-in.
// DaysSoFar is 7 for past weeks and the days from Monday up to today for the current week.
public sealed record CheckInWeek(
    DateOnly WeekStart,
    DateOnly? PreviousWeekStart,
    DateOnly? NextWeekStart,
    bool IsCurrentWeek,
    IReadOnlyList<CheckInDay> Days,
    int CheckInCount,
    int DaysSoFar,
    RatingAverages? Averages,
    RatingAverages? PreviousAverages,
    IReadOnlyList<FeelingCount> TopFeelings);

// CheckIn is null when she has no check-in that day. Tags are the ones she ticked, in tag order.
public sealed record CheckInDay(
    DateOnly Date,
    DailyCheckIn? CheckIn,
    IReadOnlyList<FeelingTag> Tags,
    bool IsFuture,
    bool IsEditable);

// Each rounded to one decimal.
public sealed record RatingAverages(decimal Mood, decimal SleepQuality, decimal Energy, decimal Stress);

public sealed record FeelingCount(FeelingTag Tag, int Days);
