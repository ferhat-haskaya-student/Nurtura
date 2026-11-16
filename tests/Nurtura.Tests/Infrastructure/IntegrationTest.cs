using System.Net;

namespace Nurtura.Tests.Infrastructure;

// Base for tests that run the app: every test starts from an emptied nurtura_test with the five test accounts.
[Collection(AppCollection.Name)]
public abstract class IntegrationTest(NurturaAppFactory factory) : IAsyncLifetime
{
    protected NurturaAppFactory Factory { get; } = factory;

    protected TestData Data { get; } = new(factory);

    protected TestUsers Users { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await DatabaseReset.RunAsync();
        Users = await Data.SeedUsersAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected Task<AppClient> SignInAsync(string email) => AppClient.SignedInAsync(Factory, email);

    protected AppClient Anonymous() => new(Factory);

    protected static string Date(DateOnly date) => date.ToString("dd-MM-yyyy");

    protected static void AssertRedirect(HttpResponseMessage response, string pathStart)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        var path = location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
        Assert.StartsWith(pathStart, path, StringComparison.OrdinalIgnoreCase);
    }
}
