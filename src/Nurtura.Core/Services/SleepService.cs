using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Every method takes the logged-in user's id. Sleeps are reached only through her own babies,
// so another mother's baby or sleep id is treated as not found.
public class SleepService
{
    private readonly INurturaDbContext _db;

    public SleepService(INurturaDbContext db)
    {
        _db = db;
    }

    // Null when the baby does not exist or is not hers.
    public async Task<SleepOverview?> GetOverviewAsync(string userId, int babyId, CancellationToken cancellationToken = default)
    {
        if (!await IsOwnBabyAsync(userId, babyId, cancellationToken))
        {
            return null;
        }

        var sleeps = OwnSleeps(userId).AsNoTracking().Where(s => s.BabyId == babyId);
        var running = await sleeps.SingleOrDefaultAsync(s => s.EndTime == null, cancellationToken);
        var last = await sleeps
            .Where(s => s.EndTime != null)
            .OrderByDescending(s => s.EndTime)
            .FirstOrDefaultAsync(cancellationToken);

        // Midnight always exists in Warsaw; the clocks change at night but not at 00:00.
        PolandTime.TryToUtc(PolandTime.Today.AddDays(1 - SleepRecord.RecentDays), TimeOnly.MinValue, out var since);
        var recent = await sleeps
            .Where(s => s.EndTime != null && s.StartTime >= since)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync(cancellationToken);

        var days = recent
            .GroupBy(s => DateOnly.FromDateTime(PolandTime.ToLocal(s.StartTime)))
            .Select(g => new SleepDay(g.Key, g.ToList()))
            .ToList();

        return new SleepOverview(running, last, days);
    }

    // Null when there is none, or the baby is not hers.
    public async Task<SleepRecord?> GetRunningAsync(string userId, int babyId, CancellationToken cancellationToken = default)
    {
        return await OwnSleeps(userId)
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.BabyId == babyId && s.EndTime == null, cancellationToken);
    }

    // Null when the sleep does not exist or is not hers.
    public async Task<SleepRecord?> GetAsync(string userId, int sleepId, CancellationToken cancellationToken = default)
    {
        return await OwnSleeps(userId).AsNoTracking().SingleOrDefaultAsync(s => s.Id == sleepId, cancellationToken);
    }

    // A new sleep: a running timer when the end is empty, a past sleep otherwise.
    public async Task<SleepSaveResult> CreateAsync(
        string userId,
        int babyId,
        SleepInput input,
        CancellationToken cancellationToken = default)
    {
        if (!await IsOwnBabyAsync(userId, babyId, cancellationToken))
        {
            return new SleepSaveResult(SleepSaveStatus.NotFound);
        }

        var sleep = new SleepRecord { BabyId = babyId, CreatedAt = DateTime.UtcNow };
        return await SaveAsync(sleep, input, cancellationToken);
    }

    // Stop, resume, or change the times, note and sharing of one of her sleeps.
    public async Task<SleepSaveResult> UpdateAsync(
        string userId,
        int sleepId,
        SleepInput input,
        CancellationToken cancellationToken = default)
    {
        var sleep = await OwnSleeps(userId).SingleOrDefaultAsync(s => s.Id == sleepId, cancellationToken);
        if (sleep is null)
        {
            return new SleepSaveResult(SleepSaveStatus.NotFound);
        }

        return await SaveAsync(sleep, input, cancellationToken);
    }

    // Returns the baby's id, or null when the sleep does not exist or is not hers.
    public async Task<int?> DeleteAsync(string userId, int sleepId, CancellationToken cancellationToken = default)
    {
        var sleep = await OwnSleeps(userId).SingleOrDefaultAsync(s => s.Id == sleepId, cancellationToken);
        if (sleep is null)
        {
            return null;
        }

        _db.SleepRecords.Remove(sleep);
        await _db.SaveChangesAsync(cancellationToken);
        return sleep.BabyId;
    }

    // Times are kept to the minute, so what she sees, the durations and the overlap check all agree.
    public static DateTime ToMinute(DateTime utc) =>
        new(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);

    // Only the baby's latest sleep, stopped less than ResumeWindow ago, can be resumed (baby woke briefly).
    // The sleep must already be one of hers (from GetAsync).
    public async Task<bool> CanResumeAsync(SleepRecord sleep, CancellationToken cancellationToken = default)
    {
        if (sleep.EndTime is null || DateTime.UtcNow - sleep.EndTime.Value >= SleepRecord.ResumeWindow)
        {
            return false;
        }

        var latestId = await _db.SleepRecords
            .Where(s => s.BabyId == sleep.BabyId)
            .OrderByDescending(s => s.StartTime)
            .ThenByDescending(s => s.Id)
            .Select(s => s.Id)
            .FirstAsync(cancellationToken);
        return latestId == sleep.Id;
    }

    private async Task<SleepSaveResult> SaveAsync(SleepRecord sleep, SleepInput input, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var start = ToMinute(input.StartUtc);
        DateTime? end = input.EndUtc is null ? null : ToMinute(input.EndUtc.Value);

        // Clearing the end of a stopped sleep makes it run again, whichever button sent it.
        if (sleep.Id != 0 && end is null && sleep.EndTime is not null && !await CanResumeAsync(sleep, cancellationToken))
        {
            return new SleepSaveResult(SleepSaveStatus.ResumeNotAllowed);
        }

        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.ReplaceLineEndings("\n").Trim();
        if (note?.Length > SleepRecord.NoteMaxLength)
        {
            return new SleepSaveResult(SleepSaveStatus.NoteTooLong);
        }

        if (start > now || end > now)
        {
            return new SleepSaveResult(SleepSaveStatus.InFuture);
        }

        if (end <= start)
        {
            return new SleepSaveResult(SleepSaveStatus.EndBeforeStart);
        }

        // A running sleep counts until now: one left running for over a day cannot be kept running either.
        if ((end ?? now) - start > SleepRecord.MaxDuration)
        {
            return new SleepSaveResult(SleepSaveStatus.TooLong);
        }

        var others = _db.SleepRecords.AsNoTracking().Where(s => s.BabyId == sleep.BabyId && s.Id != sleep.Id);

        if (end is null)
        {
            var running = await others.FirstOrDefaultAsync(s => s.EndTime == null, cancellationToken);
            if (running is not null)
            {
                return new SleepSaveResult(SleepSaveStatus.AlreadyRunning, Clash: running);
            }
        }

        // Touching is fine (one ends at 05:08, the next starts at 05:08). A running sleep lasts until now.
        var until = end ?? now;
        var clash = await others
            .Where(s => s.StartTime < until && (s.EndTime ?? now) > start)
            .OrderBy(s => s.StartTime)
            .FirstOrDefaultAsync(cancellationToken);
        if (clash is not null)
        {
            return new SleepSaveResult(SleepSaveStatus.Overlap, Clash: clash);
        }

        sleep.StartTime = start;
        sleep.EndTime = end;
        sleep.Note = note;
        sleep.IsShared = input.IsShared;
        sleep.UpdatedAt = now;
        if (sleep.Id == 0)
        {
            _db.SleepRecords.Add(sleep);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new SleepSaveResult(SleepSaveStatus.Saved, sleep.Id);
    }

    private Task<bool> IsOwnBabyAsync(string userId, int babyId, CancellationToken cancellationToken) =>
        _db.Babies.AnyAsync(
            b => b.Id == babyId && _db.MotherProfiles.Any(p => p.Id == b.MotherProfileId && p.UserId == userId),
            cancellationToken);

    private IQueryable<SleepRecord> OwnSleeps(string userId) =>
        _db.SleepRecords.Where(s => _db.Babies.Any(
            b => b.Id == s.BabyId && _db.MotherProfiles.Any(p => p.Id == b.MotherProfileId && p.UserId == userId)));
}
