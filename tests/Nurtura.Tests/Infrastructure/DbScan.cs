using Npgsql;

namespace Nurtura.Tests.Infrastructure;

// Searches the whole of nurtura_test, table by table, using the database's own catalogue. New tables and columns
// are covered without changing the tests.
public static class DbScan
{
    public sealed record Hit(string Table, string Column, long Rows)
    {
        public override string ToString() => $"{Table}.{Column}: {Rows} row(s)";
    }

    // Every foreign-key column that points to the given table's key, with the rows holding the given value.
    public static async Task<List<Hit>> RowsReferencingAsync(string referencedTable, object value)
    {
        const string sql = """
            SELECT kcu.table_name, kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage ccu
              ON tc.constraint_name = ccu.constraint_name AND tc.table_schema = ccu.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = 'public' AND ccu.table_name = @table
            """;

        await using var connection = await OpenAsync();
        var columns = new List<(string Table, string Column)>();
        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("table", referencedTable);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        Assert.NotEmpty(columns);
        return await CountAsync(connection, columns, column => $"\"{column}\" = @value", value);
    }

    // Every text column of every table, with the rows containing the given text (case ignored).
    public static async Task<List<Hit>> RowsContainingAsync(string text)
    {
        const string sql = """
            SELECT c.table_name, c.column_name
            FROM information_schema.columns c
            JOIN information_schema.tables t ON t.table_name = c.table_name AND t.table_schema = c.table_schema
            WHERE c.table_schema = 'public' AND t.table_type = 'BASE TABLE'
              AND c.data_type IN ('text', 'character varying', 'character')
            """;

        await using var connection = await OpenAsync();
        var columns = new List<(string Table, string Column)>();
        await using (var command = new NpgsqlCommand(sql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        Assert.NotEmpty(columns);
        return await CountAsync(connection, columns, column => $"strpos(lower(\"{column}\"), lower(@value)) > 0", text);
    }

    private static async Task<List<Hit>> CountAsync(
        NpgsqlConnection connection, List<(string Table, string Column)> columns, Func<string, string> condition, object value)
    {
        var hits = new List<Hit>();
        foreach (var (table, column) in columns)
        {
            await using var command = new NpgsqlCommand($"SELECT count(*) FROM \"{table}\" WHERE {condition(column)}", connection);
            command.Parameters.AddWithValue("value", value);
            var rows = (long)(await command.ExecuteScalarAsync())!;
            if (rows > 0)
            {
                hits.Add(new Hit(table, column, rows));
            }
        }

        return hits;
    }

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(TestSettings.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
