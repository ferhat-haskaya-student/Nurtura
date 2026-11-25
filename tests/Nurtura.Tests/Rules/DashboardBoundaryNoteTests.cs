using System.Net;
using System.Text.RegularExpressions;
using Nurtura.Core;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Rules;

// The boundary note at the bottom of the mother's dashboard: the same for everyone, with charts or without, and its
// last words lead to booking.
public sealed partial class DashboardBoundaryNoteTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    [Theory]
    [InlineData("en", false)]
    [InlineData("en", true)]
    [InlineData("pl", false)]
    [InlineData("pl", true)]
    public async Task Note_FullText_LinksToBook(string language, bool withCharts)
    {
        var (text, linkText) = language == "en"
            ? ("These charts are a record of how you've felt, not a medical assessment. If something worries you, " +
               "talk to your midwife or doctor, or book a consultation.", "book a consultation")
            : ("Te wykresy pokazują, jak się czułaś, a nie ocenę medyczną. Jeśli coś Cię niepokoi, " +
               "porozmawiaj z położną lub lekarzem albo umów konsultację.", "umów konsultację");

        if (withCharts)
        {
            await Data.AddCheckInAsync(Users.A.ProfileId, PolandTime.Today, null, shared: false);
            await Data.AddCheckInAsync(Users.A.ProfileId, PolandTime.Today.AddDays(-1), null, shared: false);
        }

        using var a = await SignInAsync(Users.A.Email);
        a.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        a.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        var html = await a.GetHtmlAsync("/Mother");
        Assert.Equal(withCharts, html.Contains("wellbeing-chart-data"));

        var note = Assert.Single(NotePattern().Matches(html)).Groups[1].Value;
        Assert.Equal(text, Text(note));

        var link = Assert.Single(LinkPattern().Matches(note));
        Assert.Equal("/Mother/Book", link.Groups[1].Value);
        Assert.Equal(linkText, Text(link.Groups[2].Value));
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync(link.Groups[1].Value)).StatusCode);
    }

    // What the browser shows: no tags, and the indentation around the text is not seen.
    private static string Text(string html) => WebUtility.HtmlDecode(TagPattern().Replace(html, "")).Trim();

    [GeneratedRegex("<p class=\"small text-body-secondary border-top pt-3 mb-4\">(.*?)</p>", RegexOptions.Singleline)]
    private static partial Regex NotePattern();

    [GeneratedRegex("<a [^>]*href=\"([^\"]*)\"[^>]*>(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex LinkPattern();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagPattern();
}
