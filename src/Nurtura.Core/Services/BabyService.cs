using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Every method takes the logged-in user's id and works only on that mother's own babies.
// Babies are reached by id in URLs, so every query goes through OwnBabies.
public class BabyService
{
    private const int FirstStageDays = 42;

    private readonly INurturaDbContext _db;

    public BabyService(INurturaDbContext db)
    {
        _db = db;
    }

    // Oldest first.
    public async Task<List<Baby>> GetBabiesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await OwnBabies(userId)
            .AsNoTracking()
            .OrderBy(b => b.BirthDate)
            .ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);
    }

    // Null when the baby does not exist or is not hers.
    public async Task<Baby?> GetAsync(string userId, int babyId, CancellationToken cancellationToken = default)
    {
        return await OwnBabies(userId).AsNoTracking().SingleOrDefaultAsync(b => b.Id == babyId, cancellationToken);
    }

    // Returns the new baby's id, or null when the user has no mother profile.
    public async Task<int?> AddAsync(string userId, BabyDetails details, CancellationToken cancellationToken = default)
    {
        details.EnsureValid(PolandTime.Today);

        var profileId = await _db.MotherProfiles
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (profileId is null)
        {
            return null;
        }

        var baby = new Baby { MotherProfileId = profileId.Value };
        details.CopyTo(baby);
        _db.Babies.Add(baby);
        await _db.SaveChangesAsync(cancellationToken);
        return baby.Id;
    }

    // False when the baby does not exist or is not hers.
    public async Task<bool> UpdateAsync(
        string userId,
        int babyId,
        BabyDetails details,
        CancellationToken cancellationToken = default)
    {
        details.EnsureValid(PolandTime.Today);

        var baby = await OwnBabies(userId).SingleOrDefaultAsync(b => b.Id == babyId, cancellationToken);
        if (baby is null)
        {
            return false;
        }

        details.CopyTo(baby);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Deletes the profile; its sleep and feeding records go with it (cascade).
    // False when the baby does not exist or is not hers.
    public async Task<bool> RemoveAsync(string userId, int babyId, CancellationToken cancellationToken = default)
    {
        var baby = await OwnBabies(userId).SingleOrDefaultAsync(b => b.Id == babyId, cancellationToken);
        if (baby is null)
        {
            return false;
        }

        _db.Babies.Remove(baby);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // The most recent birth; twins are told apart by the later-added profile.
    public static Baby? Youngest(IEnumerable<Baby> babies)
    {
        return babies.OrderByDescending(b => b.BirthDate).ThenByDescending(b => b.Id).FirstOrDefault();
    }

    public static PostpartumStage StageFor(DateOnly birthDate, DateOnly today)
    {
        if (today.DayNumber - birthDate.DayNumber < FirstStageDays)
        {
            return PostpartumStage.FirstSixWeeks;
        }

        var age = BabyAge.Calculate(birthDate, today);
        var months = age.Years * 12 + age.Months;
        return months switch
        {
            < 3 => PostpartumStage.SixWeeksToThreeMonths,
            < 6 => PostpartumStage.ThreeToSixMonths,
            < 12 => PostpartumStage.SixToTwelveMonths,
            _ => PostpartumStage.OverOneYear
        };
    }

    // From her youngest baby. Null when she has no baby.
    public async Task<PostpartumStage?> GetPostpartumStageAsync(string userId, CancellationToken cancellationToken = default)
    {
        var birthDate = await OwnBabies(userId)
            .OrderByDescending(b => b.BirthDate)
            .Select(b => (DateOnly?)b.BirthDate)
            .FirstOrDefaultAsync(cancellationToken);

        return birthDate is null ? null : StageFor(birthDate.Value, PolandTime.Today);
    }

    private IQueryable<Baby> OwnBabies(string userId) =>
        _db.Babies.Where(b => _db.MotherProfiles.Any(p => p.Id == b.MotherProfileId && p.UserId == userId));
}
