using System.Net;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.DataProtection;

// Every response carries the security headers: on a public page, a signed-in page with her own data, an error
// page and a static file.
public sealed class SecurityHeadersTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        string Header(string name) =>
            string.Join(",", response.Headers.TryGetValues(name, out var values) ? values
                : response.Content.Headers.TryGetValues(name, out var contentValues) ? contentValues : []);

        Assert.Equal("DENY", Header("X-Frame-Options"));
        Assert.Equal("frame-ancestors 'none'", Header("Content-Security-Policy"));
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
}
