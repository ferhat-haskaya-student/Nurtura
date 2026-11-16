using Microsoft.EntityFrameworkCore;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.DataProtection;

// The Nurtura Assistant keeps counts only: what a mother typed is stored nowhere, not in any table and not in the logs.
public sealed class AssistantPrivacyTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Search_StoresNoSearchText()
    {
        var marker = TestData.Marker("SearchText");
        using var a = await SignInAsync(Users.A.Email);

        var response = await a.PostAsync("/Mother/Assistant", new()
        {
            ["search"] = $"{marker} heavy bleeding since this morning", ["from"] = "guides"
        });
        var html = await response.Content.ReadAsStringAsync();

        // Allowed side: the search really ran, showed the urgent card and was counted.
        Assert.True(response.IsSuccessStatusCode, $"Search returned {response.StatusCode}");
        Assert.Contains("call 112", html);
        var queries = await Data.DbAsync(db => db.AssistantQueries.AsNoTracking().ToListAsync());
        Assert.True(Assert.Single(queries).UrgentShown);

        // Refused side: the text is nowhere. (The log capture does receive the app's logs, so the check is real.)
        Assert.Empty(await DbScan.RowsContainingAsync(marker));
        Assert.Contains(Factory.Logs.Lines, line => line.Contains("/Mother/Assistant"));
        Assert.DoesNotContain(Factory.Logs.Lines, line => line.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
