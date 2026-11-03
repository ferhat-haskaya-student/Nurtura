namespace Nurtura.Core.Entities;

// One sleep of one baby. Times are UTC, cut to whole minutes; shown in Warsaw time.
// A sleep with no end time is running. Each baby has at most one running sleep,
// and sleeps of the same baby never overlap.
public class SleepRecord
{
    public const int NoteMaxLength = 500;

    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(24);

    // After this long running, she is asked whether she forgot to stop it. Nothing stops automatically.
    public static readonly TimeSpan LongRunning = TimeSpan.FromHours(12);

    // Resume is for a baby who woke briefly: only the baby's latest sleep, and only this soon after it ended.
    public static readonly TimeSpan ResumeWindow = TimeSpan.FromHours(1);

    // The baby page lists the sleeps that started today and the days before, this many days in all.
    public const int RecentDays = 7;

    public int Id { get; set; }

    public int BabyId { get; set; }

    // UTC.
    public DateTime StartTime { get; set; }

    // UTC. Empty while the timer runs.
    public DateTime? EndTime { get; set; }

    public string? Note { get; set; }

    // Private by default: only a professional she books can see a shared sleep.
    public bool IsShared { get; set; }

    // UTC.
    public DateTime CreatedAt { get; set; }

    // UTC.
    public DateTime UpdatedAt { get; set; }
}
