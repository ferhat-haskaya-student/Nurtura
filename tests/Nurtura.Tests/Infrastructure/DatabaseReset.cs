using Npgsql;

namespace Nurtura.Tests.Infrastructure;

// Empties nurtura_test before every integration test, so each test starts from the same state.
// Kept: the migration history, the fixed lists inserted by migrations, the roles and the guidance pages
// with their keywords (seeded once when the app starts).
public static class DatabaseReset
{
    private static readonly string[] Kept =
    [
        "__EFMigrationsHistory",
        "FeelingTags",
        "ForumCategories",
        "AspNetRoles",
        "GuidanceCategories",
        "GuidanceContents",
        "GuidanceSources",
        "ContentKeywordTags"
    ];

    public static async Task RunAsync()
    {
        await using var connection = new NpgsqlConnection(TestSettings.ConnectionString);
        await connection.OpenAsync();

        // Checked again on the open connection, right before anything is deleted.
        if (connection.Database != TestSettings.DatabaseName)
        {
            throw new InvalidOperationException($"Refusing to reset a database other than '{TestSettings.DatabaseName}'.");
        }

        var tables = new List<string>();
        await using (var list = new NpgsqlCommand(
                         "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename", connection))
        await using (var reader = await list.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var table = reader.GetString(0);
                if (!Kept.Contains(table))
                {
                    tables.Add(table);
                }
            }
        }

        var sql = "TRUNCATE TABLE " + string.Join(", ", tables.Select(t => $"\"{t}\"")) + " RESTART IDENTITY CASCADE";
        await using var truncate = new NpgsqlCommand(sql, connection);
        await truncate.ExecuteNonQueryAsync();
    }
}
