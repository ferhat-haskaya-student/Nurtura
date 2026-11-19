using System.Net;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Access;

// An empty error response shows a friendly, translated page in the app's layout, and keeps its status code, so the
// access tests that expect 404 still mean "refused". Another mother's record looks the same as a missing page.
public sealed class StatusPageTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private static async Task<string> AssertStatusPageAsync(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"<h1 class=\"h3\">{title}</h1>", html);
        return html;
    }

    [Fact]
    public async Task MissingPage_NotSignedIn_PageNotFound()
    {
        using var client = Anonymous();

        await AssertStatusPageAsync(await client.GetAsync("/No/Such/Page"), HttpStatusCode.NotFound, "Page not found");
    }

    [Fact]
    public async Task OtherMothersPost_PageNotFound_SameAsMissingPage()
    {
        var post = await Data.AddPostAsync(Users.A.ProfileId, TestData.Marker("TitleA"), TestData.Marker("BodyA"));
        using var b = await SignInAsync(Users.B.Email);

        var html = await AssertStatusPageAsync(await b.GetAsync($"/Mother/Forum/Edit/{post}"), HttpStatusCode.NotFound, "Page not found");
        Assert.DoesNotContain(TestData.Marker("TitleA"), html);
    }

    [Fact]
    public async Task MissingPage_Polish_Translated()
    {
        using var client = Anonymous();
        var request = new HttpRequestMessage(HttpMethod.Get, "/No/Such/Page");
        request.Headers.AcceptLanguage.ParseAdd("pl");

        await AssertStatusPageAsync(await client.Http.SendAsync(request), HttpStatusCode.NotFound, "Nie znaleziono strony");
    }

    [Fact]
    public async Task BadRequest_FriendlyErrorPage_NoTechnicalDetails()
    {
        using var client = Anonymous();

        // A form posted without its antiforgery token is refused with 400.
        var response = await client.Http.PostAsync("/Account/Login", new FormUrlEncodedContent([]));

        var html = await AssertStatusPageAsync(response, HttpStatusCode.BadRequest, "Something went wrong");
        Assert.DoesNotContain("Development", html);
    }

    [Fact]
    public async Task ChatHub_MissingAddress_StaysEmpty()
    {
        using var client = Anonymous();

        var response = await client.GetAsync("/hubs/no-such-hub");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }
}
