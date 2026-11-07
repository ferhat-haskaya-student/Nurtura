namespace Nurtura.Core.Entities;

// A keyword on a guidance page, matched by the Nurtura Assistant (step 6).
public class ContentKeywordTag
{
    public const int KeywordMaxLength = 100;

    public int Id { get; set; }

    public int GuidanceContentId { get; set; }

    public string Keyword { get; set; } = string.Empty;
}
