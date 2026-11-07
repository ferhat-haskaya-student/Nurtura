using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Guidance pages for mothers. Pages are shared content; saving is hers only: every saved-page query goes
// through her own mother profile, found by the logged-in user's id. Only published pages are ever shown.
public class GuidanceService
{
    public const int SuggestedMax = 4;

    private readonly INurturaDbContext _db;

    public GuidanceService(INurturaDbContext db)
    {
        _db = db;
    }

    // The Guides page. preferredBabyId is the baby she last chose in the baby section; it is used only if it
    // is one of her babies, else her youngest baby is used.
    public async Task<GuidanceList> GetListAsync(
        string userId,
        int? preferredBabyId,
        CancellationToken cancellationToken = default)
    {
        var pages = await Published().AsNoTracking().ToListAsync(cancellationToken);

        var concerns = await _db.MotherConcerns
            .Where(c => _db.MotherProfiles.Any(p => p.Id == c.MotherProfileId && p.UserId == userId))
            .Select(c => c.Concern)
            .ToListAsync(cancellationToken);

        var babies = await _db.Babies
            .Where(b => _db.MotherProfiles.Any(p => p.Id == b.MotherProfileId && p.UserId == userId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var baby = babies.SingleOrDefault(b => b.Id == preferredBabyId) ?? BabyService.Youngest(babies);

        int? ageMonths = null;
        if (baby is not null)
        {
            var age = BabyAge.Calculate(baby.BirthDate, PolandTime.Today);
            ageMonths = age.Years * 12 + age.Months;
        }

        return new GuidanceList(
            Suggest(pages, concerns, ageMonths).Select(ToCard).ToList(),
            Group(pages, GuidanceAudience.Mother),
            Group(pages, GuidanceAudience.Baby));
    }

    // "Suggested for you": from her onboarding concerns and her selected baby's age, never from check-ins.
    // A page with a baby-age range (the nutrition stages) is suggested when the baby's age in full months
    // is in it (from inclusive, to exclusive). A page without one is suggested when its topic is one of
    // her concerns. Age matches first, then her own pages, then baby pages; at most four.
    public static List<GuidanceContent> Suggest(
        IEnumerable<GuidanceContent> pages,
        IReadOnlyCollection<Topic> concerns,
        int? babyAgeMonths)
    {
        return pages
            .Where(p => p.HasAgeRange
                ? babyAgeMonths is { } months && p.BabyAgeFromMonths <= months && months < p.BabyAgeToMonths
                : concerns.Contains(p.RelatedTopic))
            .OrderBy(p => p.HasAgeRange ? 0 : 1)
            .ThenBy(p => p.Audience == GuidanceAudience.Mother ? 0 : 1)
            .ThenBy(p => p.Title)
            .Take(SuggestedMax)
            .ToList();
    }

    // Null when the page does not exist or is not published.
    public async Task<GuidancePage?> GetPageAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var page = await Published()
            .AsNoTracking()
            .Include(g => g.Sources.OrderBy(s => s.SortOrder))
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (page is null)
        {
            return null;
        }

        var isSaved = await OwnSaved(userId).AnyAsync(s => s.GuidanceContentId == id, cancellationToken);
        return new GuidancePage(page, isSaved);
    }

    // Her saved pages, most recently saved first.
    public async Task<List<GuidanceCard>> GetSavedAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await OwnSaved(userId)
            .Join(_db.GuidanceContents.Where(g => g.IsPublished), s => s.GuidanceContentId, g => g.Id, (s, g) => new { s.SavedAt, s.Id, Page = g })
            .OrderByDescending(x => x.SavedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new GuidanceCard(x.Page.Id, x.Page.Title, x.Page.Summary))
            .ToListAsync(cancellationToken);
    }

    // False when the page does not exist or is not published, or she has no mother profile.
    // Saving a page she already saved changes nothing.
    public async Task<bool> SaveAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var profileId = await _db.MotherProfiles
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (profileId is null || !await Published().AnyAsync(g => g.Id == id, cancellationToken))
        {
            return false;
        }

        if (!await OwnSaved(userId).AnyAsync(s => s.GuidanceContentId == id, cancellationToken))
        {
            _db.SavedGuidances.Add(new SavedGuidance
            {
                MotherProfileId = profileId.Value,
                GuidanceContentId = id,
                SavedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    // False when the page does not exist or is not published. Unsaving a page she had not saved changes nothing.
    public async Task<bool> UnsaveAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        if (!await Published().AnyAsync(g => g.Id == id, cancellationToken))
        {
            return false;
        }

        var saved = await OwnSaved(userId).SingleOrDefaultAsync(s => s.GuidanceContentId == id, cancellationToken);
        if (saved is not null)
        {
            _db.SavedGuidances.Remove(saved);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static List<GuidanceGroup> Group(IEnumerable<GuidanceContent> pages, GuidanceAudience audience) =>
        pages.Where(p => p.Audience == audience)
            .GroupBy(p => p.Category!.Name)
            .OrderBy(g => g.Key)
            .Select(g => new GuidanceGroup(g.Key, g.OrderBy(p => p.Title).Select(ToCard).ToList()))
            .ToList();

    private static GuidanceCard ToCard(GuidanceContent page) => new(page.Id, page.Title, page.Summary);

    private IQueryable<GuidanceContent> Published() =>
        _db.GuidanceContents.Include(g => g.Category).Where(g => g.IsPublished);

    private IQueryable<SavedGuidance> OwnSaved(string userId) =>
        _db.SavedGuidances.Where(s => _db.MotherProfiles.Any(p => p.Id == s.MotherProfileId && p.UserId == userId));
}
