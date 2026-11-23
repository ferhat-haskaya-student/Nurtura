using System.Text.RegularExpressions;

namespace Nurtura.Tests.DataProtection;

// The Content-Security-Policy makes the browser ignore inline scripts, so a view that has one would silently stop
// working. This reads every view's source: scripts belong in wwwroot/js. Data blocks (application/json) are allowed.
public sealed partial class InlineScriptTests
{
    private static string WebFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Nurtura.sln")))
        {
            folder = folder.Parent;
        }

        Assert.NotNull(folder);
        return Path.Combine(folder.FullName, "src", "Nurtura.Web");
    }

    [Fact]
    public void Views_HaveNoInlineScripts()
    {
        var web = WebFolder();
        var views = new[] { "Views", "Areas" }
            .SelectMany(f => Directory.GetFiles(Path.Combine(web, f), "*.cshtml", SearchOption.AllDirectories))
            .ToList();
        Assert.NotEmpty(views);

        var withInlineScript = views
            .Where(v => InlineScript().IsMatch(File.ReadAllText(v)))
            .Select(v => Path.GetRelativePath(web, v));

        Assert.Empty(withInlineScript);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", true)]
    [InlineData("<script type=\"module\">x()</script>", true)]
    [InlineData("<button type=\"button\" onclick=\"x()\">", true)]
    [InlineData("<a href=\"javascript:x()\">", true)]
    [InlineData("<script src=\"~/js/site.js\"></script>", false)]
    [InlineData("<script type=\"application/json\" id=\"data\">{}</script>", false)]
    [InlineData("<div data-confirm=\"x\">", false)]
    public void InlineScript_Recognized(string html, bool expected)
    {
        Assert.Equal(expected, InlineScript().IsMatch(html));
    }

    [GeneratedRegex("""<script(?![^>]*\ssrc=)(?![^>]*application/json)|<[a-z][^>]*\son[a-z]+\s*=\s*["']|["']\s*javascript:""", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScript();
}
