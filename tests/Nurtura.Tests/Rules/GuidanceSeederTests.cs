using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Content;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Rules;

// On a fresh Windows clone the seeder file has Windows line endings, and so do its raw string bodies.
// They are stored with plain \n line endings anyway, like the bodies saved in the admin editor.
public sealed class GuidanceSeederTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Seed_BodyWithWindowsLineEndings_StoredWithPlainLineEndings()
    {
        // The seeder's pages are private, so one is changed in memory through reflection, and put back at the end.
        var pages = (Array)typeof(GuidanceSeeder)
            .GetField("Pages", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var page = pages.GetValue(0)!;
        var bodyProperty = page.GetType().GetProperty("Body")!;
        var slug = (string)page.GetType().GetProperty("Slug")!.GetValue(page)!;
        var original = (string)bodyProperty.GetValue(page)!;
        var windowsBody = original.ReplaceLineEndings("\r\n");
        Assert.Contains("\r\n", windowsBody);

        try
        {
            bodyProperty.SetValue(page, windowsBody);

            await Data.DbAsync(async db =>
            {
                // Never committed: nurtura_test keeps its guide pages and keywords for the other tests.
                await using var transaction = await db.Database.BeginTransactionAsync();
                Assert.Equal(1, await db.GuidanceContents.Where(g => g.Slug == slug).ExecuteDeleteAsync());

                var added = await GuidanceSeeder.SeedAsync(db);

                Assert.Equal([slug], added);
                var stored = await db.GuidanceContents.AsNoTracking()
                    .Where(g => g.Slug == slug)
                    .Select(g => g.Body)
                    .SingleAsync();
                Assert.DoesNotContain("\r", stored);
                Assert.Equal(original.ReplaceLineEndings("\n"), stored);
            });
        }
        finally
        {
            bodyProperty.SetValue(page, original);
        }
    }
}
