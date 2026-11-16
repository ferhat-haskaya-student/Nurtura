using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Nurtura.Tests.Infrastructure;

// A browser stand-in: keeps cookies, never follows redirects (tests check where they point), and fills in the
// antiforgery token on posts. One instance is one signed-in person.
public sealed partial class AppClient : IDisposable
{
    public static readonly Uri BaseAddress = new("https://localhost");

    private readonly NurturaAppFactory _factory;
    private readonly CookieContainer _cookies = new();
    private string _tokenPage = "/Account/Login";

    public AppClient(NurturaAppFactory factory)
    {
        _factory = factory;
        Http = factory.CreateDefaultClient(BaseAddress, new CookieHandler(_cookies));
        Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
    }

    public HttpClient Http { get; }

    public static async Task<AppClient> SignedInAsync(NurturaAppFactory factory, string email)
    {
        var client = new AppClient(factory);
        await client.SignInAsync(email);
        return client;
    }

    public async Task SignInAsync(string email, string? password = null)
    {
        var response = await PostAsync("/Account/Login", new()
        {
            ["Email"] = email,
            ["Password"] = password ?? NurturaAppFactory.Password,
            ["RememberMe"] = "false"
        });
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Sign-in failed for {email}: {response.StatusCode}");

        // The signed-in layout has the logout form, so the area home carries a token bound to this user.
        _tokenPage = response.Headers.Location!.OriginalString;
    }

    public Task<HttpResponseMessage> GetAsync(string url) => Http.GetAsync(url);

    public async Task<string> GetHtmlAsync(string url)
    {
        var response = await Http.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    // Posts a form with a valid antiforgery token for this user.
    public async Task<HttpResponseMessage> PostAsync(string url, Dictionary<string, string> fields)
    {
        var token = await TokenAsync();
        var form = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = token };
        return await Http.PostAsync(url, new FormUrlEncodedContent(form));
    }

    private async Task<string> TokenAsync()
    {
        var html = await GetHtmlAsync(_tokenPage);
        var match = TokenPattern().Match(html);
        Assert.True(match.Success, $"No antiforgery token on {_tokenPage}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    // A SignalR connection to the chat hub as this user (long polling over the in-memory server).
    public HubConnection Hub() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(BaseAddress, "/hubs/chat"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                var cookie = _cookies.GetCookieHeader(BaseAddress);
                if (!string.IsNullOrEmpty(cookie))
                {
                    options.Headers["Cookie"] = cookie;
                }
            })
            .Build();

    public void Dispose() => Http.Dispose();

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();

    private sealed class CookieHandler(CookieContainer cookies) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var header = cookies.GetCookieHeader(uri);
            if (!string.IsNullOrEmpty(header))
            {
                request.Headers.Remove("Cookie");
                request.Headers.Add("Cookie", header);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var setCookie in setCookies)
                {
                    cookies.SetCookies(uri, setCookie);
                }
            }

            return response;
        }
    }
}
