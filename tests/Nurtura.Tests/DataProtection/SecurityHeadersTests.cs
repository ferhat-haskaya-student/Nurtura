using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.DataProtection;

// Every response carries the security headers: on a public page, a signed-in page with her own data, an error
// page (missing page and a real crash) and a static file.
public sealed class SecurityHeadersTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";

    private const string CrashMessage = "Crash made by a test, with technical details";

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        string Header(string name) =>
            string.Join(",", response.Headers.TryGetValues(name, out var values) ? values
                : response.Content.Headers.TryGetValues(name, out var contentValues) ? contentValues : []);

        Assert.Equal("DENY", Header("X-Frame-Options"));
        Assert.Equal(ContentSecurityPolicy, Header("Content-Security-Policy"));
        Assert.Equal("nosniff", Header("X-Content-Type-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header("Referrer-Policy"));
        Assert.Equal("camera=(), microphone=(), geolocation=()", Header("Permissions-Policy"));
    }

    [Fact]
    public async Task PublicPage_HasSecurityHeaders()
    {
        using var client = Anonymous();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task SignedInMotherPage_HasSecurityHeaders()
    {
        using var a = await SignInAsync(Users.A.Email);

        var response = await a.GetAsync("/Mother");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task PageNotFound_HasSecurityHeaders()
    {
        using var client = Anonymous();

        var response = await client.GetAsync("/No/Such/Page");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task StaticFile_HasSecurityHeaders()
    {
        using var client = Anonymous();

        var response = await client.GetAsync("/js/site.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    // The same app with one page made to fail, so the real error handler runs.
    [Fact]
    public async Task Crash_FriendlyErrorPage_WithSecurityHeaders_NoTechnicalDetails()
    {
        using var crashing = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<MvcOptions>(options => options.Filters.Add(new CrashOnPrivacyPage()))));
        using var client = crashing.CreateDefaultClient(AppClient.BaseAddress);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");

        var response = await client.GetAsync("/Home/Privacy");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertSecurityHeaders(response);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Something went wrong", html);
        Assert.DoesNotContain(CrashMessage, html);
        Assert.DoesNotContain("Exception", html);
    }

    private sealed class CrashOnPrivacyPage : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionDescriptor.RouteValues["action"] == "Privacy")
            {
                throw new InvalidOperationException(CrashMessage);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
