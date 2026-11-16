using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Nurtura.Tests.Infrastructure;

// The test database connection. It comes from this project's user secrets or the environment variable
// ConnectionStrings__NurturaTest, never from code. Error messages name the key, never the value.
public static class TestSettings
{
    public const string ConnectionKey = "ConnectionStrings:NurturaTest";
    public const string DatabaseName = "nurtura_test";

    private static readonly Lazy<string> Connection = new(Load);

    public static string ConnectionString => Connection.Value;

    private static string Load()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(TestSettings).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var value = config[ConnectionKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"'{ConnectionKey}' is not set. Set it in the user secrets of tests/Nurtura.Tests " +
                "or in the environment variable ConnectionStrings__NurturaTest.");
        }

        // The guard: the tests reset every table, so they refuse to run on any database but nurtura_test.
        if (new NpgsqlConnectionStringBuilder(value).Database != DatabaseName)
        {
            throw new InvalidOperationException(
                $"'{ConnectionKey}' must point to the '{DatabaseName}' database. The tests never run on any other database.");
        }

        return value;
    }
}
