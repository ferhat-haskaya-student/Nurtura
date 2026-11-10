using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Content;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The Nurtura Assistant: finds guidance pages and a consultation topic for a mother's own words.
// It never diagnoses and never blocks direct booking. The search text is used only for this one match:
// it is never stored or logged, and no mother is linked to the saved AssistantQuery row.
public class AssistantService
{
    public const int SearchMaxLength = 200;
    public const int PagesMax = 3;

    private readonly INurturaDbContext _db;
    private readonly IKeywordMatcher _matcher;

    // Topic keywords flattened, so a matcher hit (ExtraIndex) points back to its topic.
    private static readonly (Topic Topic, string Keyword)[] TopicKeywords = AssistantKeywords.Topics
        .SelectMany(t => t.Value.Select(k => (t.Key, k)))
        .ToArray();

    public AssistantService(INurturaDbContext db, IKeywordMatcher matcher)
    {
        _db = db;
        _matcher = matcher;
    }

    // Null when the search is blank.
    public async Task<AssistantResult?> SearchAsync(string? search, CancellationToken cancellationToken = default)
    {
        search = search?.Trim();
        if (string.IsNullOrEmpty(search))
        {
            return null;
        }

        if (search.Length > SearchMaxLength)
        {
            search = search[..SearchMaxLength];
        }

        var urgent = AssistantText.IsUrgent(search);
        var hits = await _matcher.MatchAsync(search, TopicKeywords.Select(t => t.Keyword).ToList(), cancellationToken);

        // Best match first: the most matching keywords, then by title.
        var counts = hits.Where(h => h.PageId is not null)
            .GroupBy(h => h.PageId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var pages = await _db.GuidanceContents
            .AsNoTracking()
            .Where(g => g.IsPublished && counts.Keys.Contains(g.Id))
            .Select(g => new { g.Id, g.Title, g.Summary, g.RelatedTopic })
            .ToListAsync(cancellationToken);
        var best = pages
            .OrderByDescending(p => counts[p.Id])
            .ThenBy(p => p.Title)
            .Take(PagesMax)
            .ToList();

        // The best page's topic; with no page, the topic with the most matching keywords; else none.
        Topic? topic = best.Count > 0
            ? best[0].RelatedTopic
            : hits.Where(h => h.ExtraIndex is not null)
                .GroupBy(h => TopicKeywords[h.ExtraIndex!.Value].Topic)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => (Topic?)g.Key)
                .FirstOrDefault();

        _db.AssistantQueries.Add(new AssistantQuery
        {
            Date = PolandTime.Today,
            Matched = best.Count > 0 || topic is not null,
            SuggestedTopic = topic,
            UrgentShown = urgent
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new AssistantResult(
            urgent,
            best.Select(p => new GuidanceCard(p.Id, p.Title, p.Summary)).ToList(),
            topic);
    }
}

public sealed record AssistantResult(bool Urgent, List<GuidanceCard> Pages, Topic? SuggestedTopic)
{
    public bool Matched => Pages.Count > 0 || SuggestedTopic is not null;
}
