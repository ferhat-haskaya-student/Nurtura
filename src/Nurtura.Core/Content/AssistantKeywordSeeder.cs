using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Content;

// Seeds the page keywords (AssistantKeywords.Pages) into ContentKeywordTag, found by page slug.
// Safe to run on every start: only keywords a page does not have yet are added, and a keyword listed twice
// for one page is added once. Slugs with no page in the database are skipped. Existing rows are never changed.
public static class AssistantKeywordSeeder
{
    public static async Task SeedAsync(INurturaDbContext db, CancellationToken cancellationToken = default)
    {
        var pageIds = await db.GuidanceContents
            .Where(g => AssistantKeywords.Pages.Keys.Contains(g.Slug))
            .ToDictionaryAsync(g => g.Slug, g => g.Id, cancellationToken);

        var existing = (await db.ContentKeywordTags
                .Select(t => new { t.GuidanceContentId, t.Keyword })
                .ToListAsync(cancellationToken))
            .Select(t => (t.GuidanceContentId, t.Keyword))
            .ToHashSet();

        foreach (var (slug, keywords) in AssistantKeywords.Pages)
        {
            if (!pageIds.TryGetValue(slug, out var pageId))
            {
                continue;
            }

            foreach (var keyword in keywords.Distinct())
            {
                if (existing.Add((pageId, keyword)))
                {
                    db.ContentKeywordTags.Add(new ContentKeywordTag { GuidanceContentId = pageId, Keyword = keyword });
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
