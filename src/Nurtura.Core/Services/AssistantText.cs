using System.Globalization;
using System.Text;
using Nurtura.Core.Content;

namespace Nurtura.Core.Services;

// The Nurtura Assistant's word rules in C#, used for the urgent words. Page and topic keywords follow the same
// rules in PostgreSQL (see PostgresKeywordMatcher); urgent phrases are checked here because their words may have
// other words between them, which PostgreSQL's phrase search cannot express.
public static class AssistantText
{
    // A word of fewer letters than this must match as a whole word.
    public const int PrefixMinLength = 4;

    // Lower case, no Polish letters (ł = l, ó = o, ą = a ...), apostrophes removed ("can't" = "cant"),
    // hyphens as spaces ("c-section" = "c section").
    public static string Normalise(string text)
    {
        var decomposed = text.ToLowerInvariant()
            .Replace("'", string.Empty)
            .Replace("’", string.Empty)
            .Replace('-', ' ')
            .Replace('ł', 'l')
            .Normalize(NormalizationForm.FormD);

        var result = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(c);
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    // The normalised words of a text: runs of letters and digits.
    public static List<string> Words(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in Normalise(text))
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }

    // Two normalised words match when they are equal, or when one starts with the other and the shorter one
    // has at least 4 letters ("placzu" matches "placz"; "napiecie" does not match "nap").
    public static bool WordsMatch(string a, string b)
    {
        if (a == b)
        {
            return true;
        }

        var (shorter, longer) = a.Length <= b.Length ? (a, b) : (b, a);
        return shorter.Length >= PrefixMinLength && longer.StartsWith(shorter, StringComparison.Ordinal);
    }

    // True when the phrase words appear in the search words in the same order; other words may come between.
    public static bool ContainsInOrder(IReadOnlyList<string> searchWords, IReadOnlyList<string> phraseWords)
    {
        if (phraseWords.Count == 0)
        {
            return false;
        }

        var next = 0;
        foreach (var word in searchWords)
        {
            if (WordsMatch(word, phraseWords[next]) && ++next == phraseWords.Count)
            {
                return true;
            }
        }

        return false;
    }

    // True when the search contains any of the urgent words or phrases.
    public static bool IsUrgent(string search)
    {
        var words = Words(search);
        return AssistantKeywords.Urgent.Any(phrase => ContainsInOrder(words, Words(phrase)));
    }
}
