using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Nurtura.Tests.Infrastructure;
using Nurtura.Web.Hubs;

namespace Nurtura.Tests.Access;

// Someone who is not signed in is sent to the login page from every page of the three areas.
public sealed class AnonymousAccessTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    // Every action of every area controller and every [Authorize] controller, read from the Web assembly,
    // so a page added later is covered without changing this test.
    public static TheoryData<string, string> ProtectedActions()
    {
        var data = new TheoryData<string, string>();
        var controllers = typeof(ChatHub).Assembly.GetTypes()
            .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<AreaAttribute>() is not null || t.GetCustomAttribute<AuthorizeAttribute>() is not null);

        var seen = new HashSet<string>();
        foreach (var controller in controllers)
        {
            var area = controller.GetCustomAttribute<AreaAttribute>()?.RouteValue;
            var name = controller.Name[..^"Controller".Length];
            var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<NonActionAttribute>() is null && !m.IsSpecialName);
            foreach (var action in actions)
            {
                var actionName = action.GetCustomAttribute<ActionNameAttribute>()?.Name ?? action.Name;
                var method = action.GetCustomAttribute<HttpMethodAttribute>()?.HttpMethods.First() ?? "GET";
                var url = $"{(area is null ? "" : "/" + area)}/{name}/{actionName}/1";
                if (seen.Add(method + url))
                {
                    data.Add(method, url);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ProtectedActions))]
    public async Task EveryAreaPage_NotSignedIn_RedirectsToLogin(string method, string url)
    {
        using var client = Anonymous();

        var response = await client.Http.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = method == "GET" ? null : new FormUrlEncodedContent([])
        });

        AssertRedirect(response, "/Account/Login");
    }

    [Theory]
    [InlineData("/Mother")]
    [InlineData("/Professional")]
    [InlineData("/Admin")]
    public async Task AreaHome_NotSignedIn_RedirectsToLogin(string url)
    {
        using var client = Anonymous();

        AssertRedirect(await client.GetAsync(url), "/Account/Login");
    }

    [Fact]
    public async Task LoginPage_NotSignedIn_Opens()
    {
        using var client = Anonymous();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).StatusCode);
    }

    [Fact]
    public async Task ChatHub_NotSignedIn_Refused()
    {
        using var client = Anonymous();
        await using var hub = client.Hub();

        await Assert.ThrowsAsync<HttpRequestException>(() => hub.StartAsync());
    }
}
