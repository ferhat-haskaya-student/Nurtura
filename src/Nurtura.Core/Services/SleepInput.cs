using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// What the timer screen sends. Times are UTC; an empty end means the timer runs.
public sealed record SleepInput(DateTime StartUtc, DateTime? EndUtc, string? Note, bool IsShared);

public enum SleepSaveStatus
{
    Saved,
    NotFound,
    InFuture,
    EndBeforeStart,
    TooLong,
    AlreadyRunning,
    Overlap,
    NoteTooLong,
    ResumeNotAllowed
}

// Id is the saved sleep's id. Clash is the sleep it overlaps (Overlap) or the one already running (AlreadyRunning).
public sealed record SleepSaveResult(SleepSaveStatus Status, int? Id = null, SleepRecord? Clash = null);

// The baby page: the running sleep, the last finished one, and the recent finished ones grouped by the
// Warsaw day they started, newest first.
public sealed record SleepOverview(SleepRecord? Running, SleepRecord? Last, IReadOnlyList<SleepDay> Recent);

public sealed record SleepDay(DateOnly Date, IReadOnlyList<SleepRecord> Sleeps);
