using System.Net;
using System.Text.RegularExpressions;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Accessibility;

// The menu for keyboard and screen reader users: at most one link is marked as the current page or section, the
// phone menu button names the menu it opens, and a skip link leads past the menu.
public sealed partial class MenuTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    // Every marked link in the menu, as "address value". Only the header: pages may have tabs with their own marks.
    private static string[] Marked(string html) =>
        [.. LinkPattern().Matches(html[..html.IndexOf("</header>", StringComparison.Ordinal)])
            .Select(m => m.Value)
            .Where(tag => tag.Contains("aria-current="))
            .Select(tag => $"{Attribute(tag, "href")} {Attribute(tag, "aria-current")}")];

    private static string Attribute(string tag, string name) => Regex.Match(tag, $"{name}=\"([^\"]*)\"").Groups[1].Value;

    [Theory]
    [InlineData("/Mother", "/ page")]
    [InlineData("/Mother/Baby", "/Mother/Baby page")]
    [InlineData("/mother/baby", "/Mother/Baby page")]
    [InlineData("/Mother/Baby/Add", "/Mother/Baby true")]
    [InlineData("/Mother/Guides", "/Mother/Guides page")]
    [InlineData("/Mother/Forum", "/Mother/Forum page")]
    [InlineData("/Mother/Book", "/Mother/Book page")]
    [InlineData("/Mother/Appointments", "/Mother/Appointments page")]
    [InlineData("/Mother/MyAccount", "/Mother/MyAccount page")]
    [InlineData("/Notifications", "/Notifications page")]
    [InlineData("/Home/Privacy", "/Home/Privacy page")]
    public async Task Mother_OnlyCurrentLinkMarked(string url, string expected)
    {
        using var a = await SignInAsync(Users.A.Email);

        Assert.Equal([expected], Marked(await a.GetHtmlAsync(url)));
    }

    [Fact]
    public async Task Mother_FeedingScreen_MarksBaby()
    {
        using var a = await SignInAsync(Users.A.Email);

        Assert.Equal(["/Mother/Baby true"], Marked(await a.GetHtmlAsync($"/Mother/Feeding/Log/{Users.A.BabyId}")));
    }

    [Theory]
    [InlineData("/Admin", "/Admin page")]
    [InlineData("/Admin/Professionals", "/Admin/Professionals page")]
    [InlineData("/Admin/Reports", "/Admin/Reports page")]
    [InlineData("/Admin/Guides", "/Admin/Guides page")]
    public async Task Admin_OnlyCurrentLinkMarked(string url, string expected)
    {
        using var admin = await SignInAsync(Users.AdminEmail);

        Assert.Equal([expected], Marked(await admin.GetHtmlAsync(url)));
    }

    [Fact]
    public async Task Professional_Home_Marked()
    {
        using var p = await SignInAsync(Users.P1.Email);

        Assert.Equal(["/ page"], Marked(await p.GetHtmlAsync("/Professional")));
    }

    [Theory]
    [InlineData("/", "/ page")]
    [InlineData("/Account/Login", "/Account/Login page")]
    [InlineData("/Account/Register", "/Account/Register page")]
    public async Task NotSignedIn_OnlyCurrentLinkMarked(string url, string expected)
    {
        using var client = Anonymous();

        Assert.Equal([expected], Marked(await client.GetHtmlAsync(url)));
    }

    [Fact]
    public async Task PageNotFound_NothingMarked()
    {
        using var client = Anonymous();

        var response = await client.GetAsync("/No/Such/Page");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(Marked(await response.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData("en", "Skip to content")]
    [InlineData("pl", "Przejdź do treści")]
    public async Task SkipLink_FirstLinkOnThePage_LeadsToContent(string language, string text)
    {
        using var client = Anonymous();
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        var html = await client.GetHtmlAsync("/");

        var first = Regex.Match(html, "<a [^>]*>([^<]*)</a>");
        Assert.Equal("#main-content", Attribute(first.Value, "href"));
        Assert.Equal(text, WebUtility.HtmlDecode(first.Groups[1].Value));
        Assert.Single(Regex.Matches(html, "id=\"main-content\""));
    }

    [Fact]
    public async Task MenuButton_NamesTheMenuItOpens()
    {
        using var client = Anonymous();

        var html = await client.GetHtmlAsync("/");

        var id = Attribute(Regex.Match(html, "<button [^>]*class=\"navbar-toggler\"[^>]*>").Value, "aria-controls");
        Assert.NotEmpty(id);
        Assert.Single(Regex.Matches(html, $"id=\"{id}\""));
        Assert.Contains($"data-bs-target=\"#{id}\"", html);
    }

    [GeneratedRegex("<a [^>]*class=\"nav-link[^>]*>")]
    private static partial Regex LinkPattern();
}
