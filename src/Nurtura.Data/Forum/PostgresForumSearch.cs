using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Services;

namespace Nurtura.Data.Forum;

// Forum search with the same PostgreSQL full-text approach as the Nurtura Assistant (see PostgresKeywordMatcher):
// the 'simple' configuration (no stemming) and unaccent (ł = l, ó = o ...), apostrophes removed and hyphens read
// as spaces, on both the search and the posts.
//
// A post matches when it contains at least one search word: words of 4+ letters by beginning (word:*), 3-letter
// words whole, 1- and 2-letter words ignored. Best first: ts_rank (more matching words rank higher), then newest.
//
// The posts' text is turned into a tsvector on every search, without an index: unaccent is not immutable, so it
// cannot be used in an index expression as is, and the forum is small. The search text is only ever a bound
// parameter: it is not part of the SQL text, and EF does not log parameter values (sensitive data logging is off).
public class PostgresForumSearch : IForumSearch
{
    private readonly NurturaDbContext _db;

    public PostgresForumSearch(NurturaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<int>> SearchAsync(
        string search,
        int motherProfileId,
        int max,
        CancellationToken cancellationToken = default)
    {
        return await _db.Database.SqlQuery<int>($$"""
            WITH s AS (
                SELECT to_tsvector('simple', unaccent(translate(lower({{search}}), '-''’', ' '))) AS v
            ),
            q AS (
                SELECT to_tsquery('simple', string_agg(
                    '''' || replace(replace(w, '\', '\'), '''', '''''') || ''''
                        || CASE WHEN length(w) >= 4 THEN ':*' ELSE '' END,
                    ' | ')) AS q
                FROM s, unnest(tsvector_to_array(s.v)) AS w
                WHERE length(w) >= 3
            ),
            d AS (
                SELECT p."Id", p."CreatedAt",
                       to_tsvector('simple', unaccent(translate(lower(p."Title" || ' ' || p."Body"), '-''’', ' '))) AS v
                FROM "ForumPosts" p
                WHERE p."DeletedByAuthorAt" IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM "HiddenPosts" h
                      WHERE h."ForumPostId" = p."Id" AND h."MotherProfileId" = {{motherProfileId}})
            )
            SELECT d."Id" AS "Value"
            FROM d, q
            WHERE d.v @@ q.q
            ORDER BY ts_rank(d.v, q.q) DESC, d."CreatedAt" DESC, d."Id" DESC
            LIMIT {{max}}
            """).ToListAsync(cancellationToken);
    }
}
