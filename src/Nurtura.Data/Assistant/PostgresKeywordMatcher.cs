using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Services;

namespace Nurtura.Data.Assistant;

// The Nurtura Assistant's matcher, using PostgreSQL full-text search with the 'simple' configuration
// (splits words and lowers case, no stemming: PostgreSQL 18 has no Polish stemmer) and the unaccent
// extension (ł = l, ó = o ...). Apostrophes are removed and hyphens read as spaces, on both sides.
//
// A keyword matches when either:
// - the search's tsvector matches the keyword as a tsquery: words of 4+ letters as prefixes (word:*), shorter
//   words whole, joined by the phrase operator <-> (next to each other, in order). This covers "the search word
//   starts with the keyword" ("placzu" -> "placz"), whole-word short keywords ("nap") and multi-word keywords; or
// - for a one-word keyword: the keyword's tsvector matches the search words of 4+ letters as prefixes. This
//   covers "the keyword starts with the search word" ("stres" -> "stress").
//
// The search text is only ever a bound parameter: it is not part of the SQL text, and EF does not log
// parameter values (sensitive data logging is off).
public class PostgresKeywordMatcher : IKeywordMatcher
{
    private readonly NurturaDbContext _db;

    public PostgresKeywordMatcher(NurturaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<KeywordHit>> MatchAsync(
        string search,
        IReadOnlyList<string> extraKeywords,
        CancellationToken cancellationToken = default)
    {
        var extras = extraKeywords.ToArray();
        var rows = await _db.Database.SqlQuery<HitRow>($$"""
            WITH s AS (
                SELECT to_tsvector('simple', unaccent(translate(lower({{search}}), '-''’', ' '))) AS v
            ),
            r AS (
                SELECT to_tsquery('simple', string_agg(
                    '''' || replace(replace(w, '\', '\\'), '''', '''''') || ''':*', ' | ')) AS q
                FROM s, unnest(tsvector_to_array(s.v)) AS w
                WHERE length(w) >= 4
            ),
            k AS (
                SELECT t."GuidanceContentId" AS page_id, NULL::int AS extra_index, t."Keyword" AS keyword
                FROM "ContentKeywordTags" t
                JOIN "GuidanceContents" g ON g."Id" = t."GuidanceContentId" AND g."IsPublished"
                UNION ALL
                SELECT NULL::int, (e.ord - 1)::int, e.keyword
                FROM unnest({{extras}}::text[]) WITH ORDINALITY AS e(keyword, ord)
            ),
            -- The keyword's tsquery text is like 'how' <-> 'often': ':*' is added after each quoted lexeme of 4+
            -- letters. Lexemes never contain spaces, so the pattern cannot span the ' <-> ' between two lexemes.
            n AS (
                SELECT page_id, extra_index, unaccent(translate(lower(keyword), '-''’', ' ')) AS nk FROM k
            )
            SELECT n.page_id AS "PageId", n.extra_index AS "ExtraIndex"
            FROM n, s, r
            WHERE s.v @@ regexp_replace(
                      phraseto_tsquery('simple', n.nk)::text, '''([^'' ]{4,})''', '''\1'':*', 'g')::tsquery
               OR (strpos(btrim(n.nk), ' ') = 0 AND to_tsvector('simple', n.nk) @@ r.q)
            """).ToListAsync(cancellationToken);

        return rows.Select(r => new KeywordHit(r.PageId, r.ExtraIndex)).ToList();
    }

    // The shape EF reads each row into.
    public sealed class HitRow
    {
        public int? PageId { get; set; }

        public int? ExtraIndex { get; set; }
    }
}
