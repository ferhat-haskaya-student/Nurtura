namespace Nurtura.Core.Services;

// The Nurtura Assistant's matching layer: which keywords match a search text. The PostgreSQL full-text
// implementation lives in Nurtura.Data; another matcher can replace it without changing AssistantService.
public interface IKeywordMatcher
{
    // Matches the search against the keywords of every published guidance page and against extraKeywords.
    // One hit per matching keyword: PageId for a page keyword, ExtraIndex (index in extraKeywords) otherwise.
    // The search text must never be stored or logged.
    Task<IReadOnlyList<KeywordHit>> MatchAsync(
        string search,
        IReadOnlyList<string> extraKeywords,
        CancellationToken cancellationToken = default);
}

public sealed record KeywordHit(int? PageId, int? ExtraIndex);
