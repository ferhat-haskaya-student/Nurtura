using System.Text;

namespace Nurtura.Core.Services;

// The fixed slug of a new guidance page, made once from its title: lower case, Polish letters as plain letters
// ("ł" = "l"), every run of other characters as one "-". The slug never changes after that.
public static class GuidanceSlug
{
    // Leaves room for a "-2", "-3" ... ending within GuidanceContent.SlugMaxLength.
    public const int BaseMaxLength = 90;

    private const string Fallback = "page";

    public static string FromTitle(string title)
    {
        var result = new StringBuilder();
        foreach (var c in AssistantText.Normalise(title))
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                result.Append(c);
            }
            else if (result.Length > 0 && result[^1] != '-')
            {
                result.Append('-');
            }
        }

        var slug = result.ToString().Trim('-');
        if (slug.Length > BaseMaxLength)
        {
            slug = slug[..BaseMaxLength].TrimEnd('-');
        }

        return slug.Length == 0 ? Fallback : slug;
    }

    // The slug for a title that is not one of the taken slugs: the plain slug, or the first free "-2", "-3" ...
    public static string Unique(string title, IReadOnlySet<string> taken)
    {
        var slug = FromTitle(title);
        if (!taken.Contains(slug))
        {
            return slug;
        }

        for (var n = 2; ; n++)
        {
            var candidate = $"{slug}-{n}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
