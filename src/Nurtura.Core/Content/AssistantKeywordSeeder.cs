using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Content;

// Seeds the page keywords (AssistantKeywords.Pages) into ContentKeywordTag, found by page slug, only for the pages
// GuidanceSeeder added in the same run. Pages that already existed are left alone, so a keyword the admin removed
// in the admin panel never comes back on the next start. A keyword listed twice for one page is added once.
public static class AssistantKeywordSeeder
{
    public static async Task SeedAsync(
        INurturaDbContext db,
        IReadOnlyCollection<string> newPageSlugs,
        CancellationToken cancellationToken = default)
    {
        var slugs = AssistantKeywords.Pages.Keys.Where(newPageSlugs.Contains).ToList();
        if (slugs.Count == 0)
        {
            return;
        }

        var pageIds = await db.GuidanceContents
            .Where(g => slugs.Contains(g.Slug))
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
