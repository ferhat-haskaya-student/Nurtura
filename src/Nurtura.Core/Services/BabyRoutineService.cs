using Microsoft.EntityFrameworkCore;

namespace Nurtura.Core.Services;

// The baby routine summary over the last 7 routine days (07:00 to 07:00). Worked out on request, never stored.
// The baby is reached only through her own profile, so another mother's baby id is treated as not found.
public class BabyRoutineService
{
    private readonly INurturaDbContext _db;

    public BabyRoutineService(INurturaDbContext db)
    {
        _db = db;
    }

    // Null when the baby does not exist or is not hers.
    public async Task<BabyRoutine?> GetAsync(string userId, int babyId, CancellationToken cancellationToken = default)
    {
        var isOwnBaby = await _db.Babies.AnyAsync(
            b => b.Id == babyId && _db.MotherProfiles.Any(p => p.Id == b.MotherProfileId && p.UserId == userId),
            cancellationToken);
        if (!isOwnBaby)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var since = BabyRoutine.SinceUtc(now);

        // Each sleep belongs whole to the routine day it started in. A running sleep is not counted until it stops.
        var sleeps = await _db.SleepRecords
            .AsNoTracking()
            .Where(s => s.BabyId == babyId && s.EndTime != null && s.StartTime >= since)
            .ToListAsync(cancellationToken);

        var feeds = await _db.FeedingRecords
            .AsNoTracking()
            .Where(f => f.BabyId == babyId && f.Time >= since)
            .ToListAsync(cancellationToken);

        return BabyRoutine.Build(sleeps, feeds, now);
    }
}
