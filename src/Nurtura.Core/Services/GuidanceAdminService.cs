using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Content;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The admin's Guides editor: every page (published or not), one form for new and existing pages, and "Mark as
// checked today". For the Admin role only; the caller checks the role. Every rule is checked here on the server.
// A page's slug is made once from its title when it is created and never changed afterwards, and it is never one
// of the seeder's slugs, so GuidanceSeeder (which skips any slug that exists) never overwrites an admin edit.
public class GuidanceAdminService
{
    public const int BodyMaxLength = 20000;
    public const int ItemMaxLength = 500;
    public const int AgeMaxMonths = 12;

    // How long until a page should be checked again.
    public const int CheckEveryYears = 1;

    private readonly INurturaDbContext _db;

    public GuidanceAdminService(INurturaDbContext db)
    {
        _db = db;
    }

    // Every page, published or not: by audience, then category, then title.
    public async Task<List<GuideListItem>> GetListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.GuidanceContents
            .OrderBy(g => g.Audience)
            .ThenBy(g => g.Category!.Name)
            .ThenBy(g => g.Title)
            .Select(g => new GuideListItem(g.Id, g.Title, g.Audience, g.Category!.Name, g.LastChecked, g.NextCheck, g.IsPublished))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<GuidanceCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        await _db.GuidanceCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);

    // A page as the form shows it; null when it does not exist.
    public async Task<GuideEdit?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var page = await _db.GuidanceContents
            .AsNoTracking()
            .Include(g => g.Sources.OrderBy(s => s.SortOrder))
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (page is null)
        {
            return null;
        }

        var keywords = await _db.ContentKeywordTags
            .Where(t => t.GuidanceContentId == id)
            .OrderBy(t => t.Id)
            .Select(t => t.Keyword)
            .ToListAsync(cancellationToken);

        return new GuideEdit(page.Id, page.Slug, page.LastChecked, page.NextCheck, new GuideInput(
            page.Title,
            page.Summary,
            page.Body,
            page.Audience,
            page.GuidanceCategoryId,
            page.RelatedTopic,
            page.BabyAgeFromMonths,
            page.BabyAgeToMonths,
            page.GetHelpHeading,
            string.Join("\n", page.GetHelpItems),
            page.EmergencyHeading,
            string.Join("\n", page.EmergencyItems),
            string.Join("\n", page.Sources.Select(s => $"{s.Name} | {s.Url}")),
            string.Join(", ", keywords),
            page.Language,
            page.IsPublished));
    }

    // A new page: slug made once from the title (unique, never a seeder slug), checked today, next check in a year.
    public async Task<GuideSaveResult> CreateAsync(GuideInput input, CancellationToken cancellationToken = default)
    {
        var (guide, errors) = await ParseAsync(input, cancellationToken);
        if (guide is null)
        {
            return new GuideSaveResult(0, errors);
        }

        var taken = (await _db.GuidanceContents.Select(g => g.Slug).ToListAsync(cancellationToken))
            .Concat(GuidanceSeeder.SeededSlugs)
            .ToHashSet(StringComparer.Ordinal);
        var today = PolandTime.Today;
        var page = new GuidanceContent
        {
            Slug = GuidanceSlug.Unique(guide.Title, taken),
            LastChecked = today,
            NextCheck = today.AddYears(CheckEveryYears)
        };
        Apply(page, guide);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.GuidanceContents.Add(page);
        await _db.SaveChangesAsync(cancellationToken);
        AddKeywords(page.Id, guide.Keywords);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new GuideSaveResult(page.Id, []);
    }

    // Saves the form over an existing page; its sources and keywords are replaced. The slug and the check dates
    // are never changed here.
    public async Task<GuideSaveResult> UpdateAsync(int id, GuideInput input, CancellationToken cancellationToken = default)
    {
        var page = await _db.GuidanceContents
            .Include(g => g.Sources)
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (page is null)
        {
            return new GuideSaveResult(id, [], Found: false);
        }

        var (guide, errors) = await ParseAsync(input, cancellationToken);
        if (guide is null)
        {
            return new GuideSaveResult(id, errors);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        Apply(page, guide);
        await _db.ContentKeywordTags.Where(t => t.GuidanceContentId == id).ExecuteDeleteAsync(cancellationToken);
        AddKeywords(id, guide.Keywords);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new GuideSaveResult(id, []);
    }

    // Last checked = today (Poland time), next check = a year later. False when the page does not exist.
    public async Task<bool> MarkCheckedAsync(int id, CancellationToken cancellationToken = default)
    {
        var today = PolandTime.Today;
        var nextCheck = today.AddYears(CheckEveryYears);
        var changed = await _db.GuidanceContents
            .Where(g => g.Id == id)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(g => g.LastChecked, today)
                    .SetProperty(g => g.NextCheck, nextCheck),
                cancellationToken);
        return changed == 1;
    }

    // Splits text into trimmed, non-empty lines.
    public static List<string> Lines(string? text) =>
        (text ?? string.Empty)
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

    // An absolute http or https address.
    public static bool IsWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !url.Any(char.IsWhiteSpace);

    // Checks the whole form; a page only when there are no errors.
    private async Task<(ParsedGuide? Guide, List<GuideError> Errors)> ParseAsync(GuideInput input, CancellationToken cancellationToken)
    {
        var errors = new List<GuideError>();

        var title = Required(input.Title, nameof(GuideInput.Title), GuidanceContent.TitleMaxLength, errors);
        var summary = Required(input.Summary, nameof(GuideInput.Summary), GuidanceContent.SummaryMaxLength, errors);
        var body = Required(input.Body?.ReplaceLineEndings("\n"), nameof(GuideInput.Body), BodyMaxLength, errors);
        var getHelpHeading = Required(input.GetHelpHeading, nameof(GuideInput.GetHelpHeading), GuidanceContent.HeadingMaxLength, errors);

        if (input.Audience is not { } audience || !Enum.IsDefined(audience))
        {
            errors.Add(new GuideError(nameof(GuideInput.Audience), "GuideAdmin_Error_Choose"));
        }

        if (input.RelatedTopic is not { } topic || !Enum.IsDefined(topic))
        {
            errors.Add(new GuideError(nameof(GuideInput.RelatedTopic), "GuideAdmin_Error_Choose"));
        }

        if (input.Language is not { } language || !Enum.IsDefined(language))
        {
            errors.Add(new GuideError(nameof(GuideInput.Language), "GuideAdmin_Error_Choose"));
        }

        if (input.CategoryId is not { } categoryId
            || !await _db.GuidanceCategories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            errors.Add(new GuideError(nameof(GuideInput.CategoryId), "GuideAdmin_Error_Choose"));
        }

        // Baby age: both or neither, whole months, 0 <= from < to <= 12.
        var (from, to) = (input.BabyAgeFromMonths, input.BabyAgeToMonths);
        if ((from is null) != (to is null)
            || (from is not null && (from < 0 || to > AgeMaxMonths || from >= to)))
        {
            errors.Add(new GuideError(nameof(GuideInput.BabyAgeFromMonths), "GuideAdmin_Error_Age", AgeMaxMonths));
        }

        var getHelpItems = Items(input.GetHelpItems, nameof(GuideInput.GetHelpItems), errors);
        if (getHelpItems.Count == 0)
        {
            errors.Add(new GuideError(nameof(GuideInput.GetHelpItems), "GuideAdmin_Error_AtLeastOneLine"));
        }

        // Emergency card: optional, but a heading and its items come together.
        var emergencyHeading = string.IsNullOrWhiteSpace(input.EmergencyHeading) ? null : input.EmergencyHeading.Trim();
        if (emergencyHeading is { Length: > GuidanceContent.HeadingMaxLength })
        {
            errors.Add(new GuideError(nameof(GuideInput.EmergencyHeading), "GuideAdmin_Error_MaxLength", GuidanceContent.HeadingMaxLength));
        }

        var emergencyItems = Items(input.EmergencyItems, nameof(GuideInput.EmergencyItems), errors);
        if (emergencyHeading is null && emergencyItems.Count > 0)
        {
            errors.Add(new GuideError(nameof(GuideInput.EmergencyHeading), "GuideAdmin_Error_EmergencyHeading"));
        }
        else if (emergencyHeading is not null && emergencyItems.Count == 0)
        {
            errors.Add(new GuideError(nameof(GuideInput.EmergencyItems), "GuideAdmin_Error_EmergencyItems"));
        }

        var sources = new List<(string Name, string Url)>();
        var sourceLines = Lines(input.Sources);
        for (var i = 0; i < sourceLines.Count; i++)
        {
            var parts = sourceLines[i].Split('|', 2);
            var name = parts[0].Trim();
            var url = parts.Length == 2 ? parts[1].Trim() : string.Empty;
            if (name.Length == 0 || name.Length > GuidanceSource.NameMaxLength
                || url.Length == 0 || url.Length > GuidanceSource.UrlMaxLength || !IsWebUrl(url))
            {
                errors.Add(new GuideError(nameof(GuideInput.Sources), "GuideAdmin_Error_SourceLine", i + 1,
                    GuidanceSource.NameMaxLength, GuidanceSource.UrlMaxLength));
            }
            else
            {
                sources.Add((name, url));
            }
        }

        if (sourceLines.Count == 0)
        {
            errors.Add(new GuideError(nameof(GuideInput.Sources), "GuideAdmin_Error_AtLeastOneLine"));
        }

        var keywords = new List<string>();
        foreach (var keyword in (input.Keywords ?? string.Empty).ReplaceLineEndings(" ").Split(',').Select(k => k.Trim()).Where(k => k.Length > 0))
        {
            if (keyword.Length > ContentKeywordTag.KeywordMaxLength)
            {
                errors.Add(new GuideError(nameof(GuideInput.Keywords), "GuideAdmin_Error_Keyword", keyword, ContentKeywordTag.KeywordMaxLength));
            }
            else if (!keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase))
            {
                keywords.Add(keyword);
            }
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ParsedGuide(
            title!, summary!, body!, input.Audience!.Value, input.CategoryId!.Value, input.RelatedTopic!.Value, from, to,
            getHelpHeading!, getHelpItems, emergencyHeading, emergencyItems, sources, keywords, input.Language!.Value,
            input.IsPublished), errors);
    }

    // Trimmed, 1 to max characters; null (with an error) otherwise.
    private static string? Required(string? value, string field, int max, List<GuideError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(new GuideError(field, "GuideAdmin_Error_Required"));
            return null;
        }

        if (trimmed.Length > max)
        {
            errors.Add(new GuideError(field, "GuideAdmin_Error_MaxLength", max));
            return null;
        }

        return trimmed;
    }

    // One item per non-empty line, each at most ItemMaxLength.
    private static List<string> Items(string? text, string field, List<GuideError> errors)
    {
        var lines = Lines(text);
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length > ItemMaxLength)
            {
                errors.Add(new GuideError(field, "GuideAdmin_Error_LineTooLong", i + 1, ItemMaxLength));
            }
        }

        return lines;
    }

    private static void Apply(GuidanceContent page, ParsedGuide guide)
    {
        page.Title = guide.Title;
        page.Summary = guide.Summary;
        page.Body = guide.Body;
        page.Audience = guide.Audience;
        page.GuidanceCategoryId = guide.CategoryId;
        page.RelatedTopic = guide.RelatedTopic;
        page.BabyAgeFromMonths = guide.AgeFrom;
        page.BabyAgeToMonths = guide.AgeTo;
        page.GetHelpHeading = guide.GetHelpHeading;
        page.GetHelpItems = guide.GetHelpItems;
        page.EmergencyHeading = guide.EmergencyHeading;
        page.EmergencyItems = guide.EmergencyItems;
        page.Language = guide.Language;
        page.IsPublished = guide.IsPublished;

        // Sources are replaced, in the order of the lines (the old rows are deleted through the relationship).
        page.Sources.Clear();
        page.Sources.AddRange(guide.Sources.Select((s, i) => new GuidanceSource { Name = s.Name, Url = s.Url, SortOrder = i + 1 }));
    }

    private void AddKeywords(int pageId, IEnumerable<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            _db.ContentKeywordTags.Add(new ContentKeywordTag { GuidanceContentId = pageId, Keyword = keyword });
        }
    }

    private sealed record ParsedGuide(
        string Title,
        string Summary,
        string Body,
        GuidanceAudience Audience,
        int CategoryId,
        Topic RelatedTopic,
        int? AgeFrom,
        int? AgeTo,
        string GetHelpHeading,
        List<string> GetHelpItems,
        string? EmergencyHeading,
        List<string> EmergencyItems,
        List<(string Name, string Url)> Sources,
        List<string> Keywords,
        Language Language,
        bool IsPublished);
}
