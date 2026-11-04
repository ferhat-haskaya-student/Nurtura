namespace Nurtura.Core.Services;

// One week (Monday to Sunday) of a professional's availability grid.
// PreviousWeekStart and NextWeekStart are null where the arrows stop.
// IsCopied: the open hours were filled from the week before and are not saved yet.
public sealed record AvailabilityWeek(
    DateOnly WeekStart,
    DateOnly? PreviousWeekStart,
    DateOnly? NextWeekStart,
    IReadOnlyList<AvailabilityDay> Days,
    int OpenCount,
    bool IsCopied);

public sealed record AvailabilityDay(DateOnly Date, IReadOnlyList<AvailabilityHour> Hours);

// IsPast: the hour has already started (Poland time) and can no longer be changed.
public sealed record AvailabilityHour(int Hour, bool IsOpen, bool IsPast);
