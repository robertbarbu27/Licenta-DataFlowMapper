using System.Data;
using System.Runtime.CompilerServices;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;
using Npgsql;

namespace DataFlowMapper.Connectors;

public class PostgreSqlConnector : IConnector
{
    private readonly string _connectionString;

    public PostgreSqlConnector(SourceConfig config)
    {
        _connectionString = config.ConnectionString;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
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
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var tables = new List<TableInfo>();
        await using var cmd = new NpgsqlCommand(
            "SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')",
            conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo { Schema = reader.GetString(0), Name = reader.GetString(1) });
        }
        return tables;
    }

    public async Task<List<FieldInfo>> GetSchemaAsync(string table)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var fields = new List<FieldInfo>();
        var parts = table.Split('.');
        var schemaFilter = parts.Length == 2 ? $"AND table_schema = '{parts[0]}'" : "";
        var tableName = parts.Length == 2 ? parts[1] : parts[0];
        await using var cmd = new NpgsqlCommand(
            $"SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name = '{tableName}' {schemaFilter} ORDER BY ordinal_position",
            conn);
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
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

        var schemaTable = await reader.GetSchemaTableAsync(cancellationToken);
        var chunk = CreateEmptyTable(schemaTable!);

        while (await reader.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = chunk.NewRow();
            for (int i = 0; i < reader.FieldCount; i++)
                row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
            chunk.Rows.Add(row);

            if (chunk.Rows.Count >= chunkSize)
            {
                yield return chunk;
                chunk = CreateEmptyTable(schemaTable!);
            }
        }

        if (chunk.Rows.Count > 0)
            yield return chunk;
    }

    public async Task WriteAsync(string table, DataTable data, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        // Auto-create table if it doesn't exist
        var colDefs = string.Join(", ", data.Columns.Cast<DataColumn>()
            .Select(c => $"\"{c.ColumnName}\" TEXT"));
        await using (var cmd = new NpgsqlCommand($"CREATE TABLE IF NOT EXISTS {table} ({colDefs})", conn))
            await cmd.ExecuteNonQueryAsync(cancellationToken);

        var columns = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => $"\"{c.ColumnName}\""));
        await using var writer = await conn.BeginBinaryImportAsync($"COPY {table} ({columns}) FROM STDIN (FORMAT BINARY)", cancellationToken);
        foreach (DataRow row in data.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.StartRowAsync(cancellationToken);
            foreach (var item in row.ItemArray)
                await writer.WriteAsync(item == DBNull.Value ? null : item, cancellationToken);
        }
        await writer.CompleteAsync(cancellationToken);
    }

    private static DataTable CreateEmptyTable(DataTable schemaTable)
    {
        var dt = new DataTable();
        foreach (DataRow row in schemaTable.Rows)
        {
            dt.Columns.Add(new DataColumn(
                row["ColumnName"].ToString(),
                (Type)row["DataType"]));
        }
        return dt;
    }
}
