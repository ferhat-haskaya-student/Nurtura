using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Nurtura.Web.Localization;

// Looks a key up in the current UI culture, then its parents (pl-PL -> pl), then English,
// and finally returns the key itself.
public class JsonStringLocalizer : IStringLocalizer
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _texts;

    public JsonStringLocalizer(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> texts)
    {
        _texts = texts;
    }

    public LocalizedString this[string name]
    {
        get
        {
            var value = Find(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = Find(name);
            var text = string.Format(CultureInfo.CurrentCulture, value ?? name, arguments);
            return new LocalizedString(name, text, resourceNotFound: value is null);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var cultures = includeParentCultures
            ? CulturesToSearch()
            : [CultureInfo.CurrentUICulture.Name];

        return cultures
            .SelectMany(culture => _texts.TryGetValue(culture, out var texts) ? texts.Keys : [])
            .Distinct()
            .Select(key => this[key]);
    }

    private string? Find(string name)
    {
        foreach (var culture in CulturesToSearch())
        {
            if (_texts.TryGetValue(culture, out var texts) && texts.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<string> CulturesToSearch()
    {
        for (var culture = CultureInfo.CurrentUICulture;
             culture != CultureInfo.InvariantCulture;
             culture = culture.Parent)
        {
            yield return culture.Name;
        }

        yield return SupportedCultures.Default;
    }
}
