using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Every method takes the logged-in user's id. Feeds are reached only through her own babies,
// so another mother's baby or feed id is treated as not found.
public class FeedingService
{
    private readonly INurturaDbContext _db;

    public FeedingService(INurturaDbContext db)
    {
        _db = db;
    }

    // Her latest feed for the baby; null when there is none or the baby is not hers.
    public async Task<FeedingRecord?> GetLastAsync(string userId, int babyId, CancellationToken cancellationToken = default)
    {
        return await Latest(OwnFeeds(userId).AsNoTracking().Where(f => f.BabyId == babyId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Newest first.
    public async Task<List<FeedingRecord>> GetRecentAsync(
        string userId,
        int babyId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        return await Latest(OwnFeeds(userId).AsNoTracking().Where(f => f.BabyId == babyId && f.Time >= sinceUtc))
            .ToListAsync(cancellationToken);
    }

    // Null when the feed does not exist or is not hers.
    public async Task<FeedingRecord?> GetAsync(string userId, int feedId, CancellationToken cancellationToken = default)
    {
        return await OwnFeeds(userId).AsNoTracking().SingleOrDefaultAsync(f => f.Id == feedId, cancellationToken);
    }

    // The type of her last feed for this baby, else the baby's usual feeding from the profile
    // ("both" picks breast, "starting solids" picks solids). Null when the baby is not hers.
    public async Task<FeedingDefaults?> GetDefaultsAsync(string userId, int babyId, CancellationToken cancellationToken = default)
    {
        var usual = await _db.Babies
            .Where(b => b.Id == babyId && _db.MotherProfiles.Any(p => p.Id == b.MotherProfileId && p.UserId == userId))
            .Select(b => (BabyFeedingType?)b.FeedingType)
            .SingleOrDefaultAsync(cancellationToken);
        if (usual is null)
        {
            return null;
        }

        var feeds = _db.FeedingRecords.AsNoTracking().Where(f => f.BabyId == babyId);
        var lastType = await Latest(feeds).Select(f => (FeedingType?)f.Type).FirstOrDefaultAsync(cancellationToken);
        var lastContent = await Latest(feeds.Where(f => f.Type == FeedingType.Bottle))
            .Select(f => f.BottleContent)
            .FirstOrDefaultAsync(cancellationToken);
        var lastSide = await Latest(feeds.Where(f => f.Type == FeedingType.Breast))
            .Select(f => f.BreastSide)
            .FirstOrDefaultAsync(cancellationToken);

        var type = lastType ?? usual switch
        {
            BabyFeedingType.Bottle => FeedingType.Bottle,
            BabyFeedingType.StartingSolids => FeedingType.Solid,
            _ => FeedingType.Breast
        };
        return new FeedingDefaults(type, lastContent, lastSide);
    }

    public async Task<FeedingSaveResult> CreateAsync(
        string userId,
        int babyId,
        FeedingDetails details,
        CancellationToken cancellationToken = default)
    {
        var isOwnBaby = await _db.Babies.AnyAsync(
            b => b.Id == babyId && _db.MotherProfiles.Any(p => p.Id == b.MotherProfileId && p.UserId == userId),
            cancellationToken);
        if (!isOwnBaby)
        {
            return new FeedingSaveResult(FeedingSaveStatus.NotFound);
        }

        var feed = new FeedingRecord { BabyId = babyId, CreatedAt = DateTime.UtcNow };
        return await SaveAsync(feed, details, cancellationToken);
    }

    public async Task<FeedingSaveResult> UpdateAsync(
        string userId,
        int feedId,
        FeedingDetails details,
        CancellationToken cancellationToken = default)
    {
        var feed = await OwnFeeds(userId).SingleOrDefaultAsync(f => f.Id == feedId, cancellationToken);
        if (feed is null)
        {
            return new FeedingSaveResult(FeedingSaveStatus.NotFound);
        }

        return await SaveAsync(feed, details, cancellationToken);
    }

    // Returns the baby's id, or null when the feed does not exist or is not hers.
    public async Task<int?> DeleteAsync(string userId, int feedId, CancellationToken cancellationToken = default)
    {
        var feed = await OwnFeeds(userId).SingleOrDefaultAsync(f => f.Id == feedId, cancellationToken);
        if (feed is null)
        {
            return null;
        }

        _db.FeedingRecords.Remove(feed);
        await _db.SaveChangesAsync(cancellationToken);
        return feed.BabyId;
    }

    private async Task<FeedingSaveResult> SaveAsync(FeedingRecord feed, FeedingDetails details, CancellationToken cancellationToken)
    {
        details.EnsureValid();

        var now = DateTime.UtcNow;
        if (details.TimeUtc > now)
        {
            return new FeedingSaveResult(FeedingSaveStatus.InFuture);
        }

        details.CopyTo(feed);
        feed.UpdatedAt = now;
        if (feed.Id == 0)
        {
            _db.FeedingRecords.Add(feed);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new FeedingSaveResult(FeedingSaveStatus.Saved, feed.Id);
    }

    private static IQueryable<FeedingRecord> Latest(IQueryable<FeedingRecord> feeds) =>
        feeds.OrderByDescending(f => f.Time).ThenByDescending(f => f.Id);

    private IQueryable<FeedingRecord> OwnFeeds(string userId) =>
        _db.FeedingRecords.Where(f => _db.Babies.Any(
            b => b.Id == f.BabyId && _db.MotherProfiles.Any(p => p.Id == b.MotherProfileId && p.UserId == userId)));
}
