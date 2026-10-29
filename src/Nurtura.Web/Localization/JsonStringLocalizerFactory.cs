using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace Nurtura.Web.Localization;

// Reads Resources/<culture>.json once from the build output. Every localizer shares the same texts,
// so the requested type or base name is ignored.
public class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly JsonStringLocalizer _localizer;

    public JsonStringLocalizerFactory()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Resources");
        var texts = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in SupportedCultures.All)
        {
            var path = Path.Combine(folder, culture + ".json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Translation file '{path}' not found.", path);
            }

            texts[culture] = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                ?? new Dictionary<string, string>();
        }

        _localizer = new JsonStringLocalizer(texts);
    }

    public IStringLocalizer Create(Type resourceSource) => _localizer;

    public IStringLocalizer Create(string baseName, string location) => _localizer;
}
