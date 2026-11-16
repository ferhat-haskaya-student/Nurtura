using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Nurtura.Data;
using Nurtura.Web.Hubs;

namespace Nurtura.Tests.Infrastructure;

// The real app, in memory, on the nurtura_test database. One instance for the whole run.
// ChatHub is only used to find the Web assembly (Program has no public type), so no app code changes.
public sealed class NurturaAppFactory : WebApplicationFactory<ChatHub>, IAsyncLifetime
{
    public const string AdminEmail = "admin@test.nurtura";

    // Every seeded test account uses this password. It is made up per run and exists only in nurtura_test.
    public static readonly string Password = "T" + Guid.NewGuid().ToString("N")[..15];

    public CapturingLoggerProvider Logs { get; } = new();

    public async Task InitializeAsync()
    {
        // The schema must exist before the app starts: its startup seeders read the database straight away.
        // Migrate also creates nurtura_test when it does not exist yet.
        var options = new DbContextOptionsBuilder<NurturaDbContext>().UseNpgsql(TestSettings.ConnectionString).Options;
        await using (var db = new NurturaDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        // Starts the app, then checks that it really talks to nurtura_test.
        await using var scope = Services.CreateAsyncScope();
        var appDb = scope.ServiceProvider.GetRequiredService<NurturaDbContext>();
        var database = new NpgsqlConnectionStringBuilder(appDb.Database.GetConnectionString()).Database;
        if (database != TestSettings.DatabaseName)
        {
            throw new InvalidOperationException($"The app is not using the '{TestSettings.DatabaseName}' database.");
        }
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: the Web project's user secrets (the real database) are not loaded,
        // and the development test accounts are not seeded.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Nurtura", TestSettings.ConnectionString);
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", Password);

        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(Logs);
            logging.AddFilter<CapturingLoggerProvider>(null, LogLevel.Debug);
        });

        // A second safety net: the DbContext is registered again on the test database.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<NurturaDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<NurturaDbContext>>();
            services.AddDbContext<NurturaDbContext>(o => o.UseNpgsql(TestSettings.ConnectionString));
        });
    }
}

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<NurturaAppFactory>
{
    public const string Name = "App";
}
