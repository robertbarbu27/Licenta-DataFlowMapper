using System.Data;
using DataFlowMapper.Core.Models;

namespace DataFlowMapper.Core.Interfaces;

public interface IConnector
{
    Task<bool> TestConnectionAsync();
    Task<List<TableInfo>> GetTablesAsync();
    Task<List<FieldInfo>> GetSchemaAsync(string table);
    IAsyncEnumerable<DataTable> ReadChunksAsync(string query, int chunkSize, CancellationToken cancellationToken);
    Task WriteAsync(string table, DataTable data, CancellationToken cancellationToken);
}
