using System.Data;
using System.Runtime.CompilerServices;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;
using MySql.Data.MySqlClient;

namespace DataFlowMapper.Connectors;

public class MySqlConnector : IConnector
{
    private readonly string _connectionString;

    public MySqlConnector(SourceConfig config)
    {
        _connectionString = config.ConnectionString;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<TableInfo>> GetTablesAsync()
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        var tables = new List<TableInfo>();
        await using var cmd = new MySqlCommand("SHOW FULL TABLES WHERE Table_type = 'BASE TABLE'", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(new TableInfo { Name = reader.GetString(0) });
        return tables;
    }

    public async Task<List<FieldInfo>> GetSchemaAsync(string table)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        var fields = new List<FieldInfo>();
        await using var cmd = new MySqlCommand($"DESCRIBE `{table}`", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            fields.Add(new FieldInfo
            {
                Name = reader.GetString(0),
                Type = reader.GetString(1),
                Nullable = reader.GetString(2) == "YES"
            });
        }
        return fields;
    }

    public async IAsyncEnumerable<DataTable> ReadChunksAsync(string query, int chunkSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new MySqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

        DataTable? chunk = null;

        while (await reader.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (chunk == null)
            {
                chunk = new DataTable();
                for (int i = 0; i < reader.FieldCount; i++)
                    chunk.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
            }

            var row = chunk.NewRow();
            for (int i = 0; i < reader.FieldCount; i++)
                row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
            chunk.Rows.Add(row);

            if (chunk.Rows.Count >= chunkSize)
            {
                yield return chunk;
                chunk = new DataTable();
                for (int i = 0; i < reader.FieldCount; i++)
                    chunk.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
            }
        }

        if (chunk != null && chunk.Rows.Count > 0)
            yield return chunk;
    }

    public async Task WriteAsync(string table, DataTable data, CancellationToken cancellationToken)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        var columns = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => $"`{c.ColumnName}`"));
        var paramNames = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => $"@{c.ColumnName}"));
        var sql = $"INSERT INTO `{table}` ({columns}) VALUES ({paramNames})";

        foreach (DataRow row in data.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var cmd = new MySqlCommand(sql, conn);
            foreach (DataColumn col in data.Columns)
                cmd.Parameters.AddWithValue($"@{col.ColumnName}", row[col] ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
