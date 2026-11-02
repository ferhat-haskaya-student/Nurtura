using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Every method takes the logged-in user's id and works only on that user's own profile.
public class MotherOnboardingService
{
    private readonly INurturaDbContext _db;

    public MotherOnboardingService(INurturaDbContext db)
    {
        _db = db;
    }

    // Null when the user has no mother profile.
    public async Task<bool?> IsCompletedAsync(string userId, CancellationToken cancellationToken = default)
    {
        var completedAt = await _db.MotherProfiles
            .Where(p => p.UserId == userId)
            .Select(p => new { p.OnboardingCompletedAt })
            .SingleOrDefaultAsync(cancellationToken);

        return completedAt is null ? null : completedAt.OnboardingCompletedAt is not null;
    }

    // Saves all onboarding answers and the first baby, and marks onboarding as done, in one transaction.
    // Returns false when the user has no mother profile or has already finished onboarding.
    public async Task<bool> CompleteAsync(
        string userId,
        OnboardingAnswers answers,
        CancellationToken cancellationToken = default)
    {
        var distinctConcerns = answers.Concerns.Distinct().ToList();
        if (distinctConcerns.Count == 0)
        {
            throw new ArgumentException("At least one concern is required.", nameof(answers));
        }

        var distinctSupportTypes = answers.SupportTypes.Distinct().ToList();
        if (distinctSupportTypes.Count == 0)
        {
            throw new ArgumentException("At least one support type is required.", nameof(answers));
        }

        answers.Baby.EnsureValid(PolandTime.Today);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var profile = await _db.MotherProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null || profile.OnboardingCompletedAt is not null)
        {
            return false;
        }

        _db.MotherConcerns.AddRange(distinctConcerns.Select(concern => new MotherConcern
        {
            MotherProfileId = profile.Id,
            Concern = concern
        }));

        _db.MotherSupportTypes.AddRange(distinctSupportTypes.Select(supportType => new MotherSupportType
        {
            MotherProfileId = profile.Id,
            SupportType = supportType
        }));

        var baby = new Baby { MotherProfileId = profile.Id };
        answers.Baby.CopyTo(baby);
        _db.Babies.Add(baby);

        profile.IsFirstBaby = answers.IsFirstBaby;
        profile.HelpAtHome = answers.HelpAtHome;
        profile.OnboardingCompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
