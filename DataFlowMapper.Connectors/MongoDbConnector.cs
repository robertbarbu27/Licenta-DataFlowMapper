using System.Data;
using System.Runtime.CompilerServices;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DataFlowMapper.Connectors;

public class MongoDbConnector : IConnector
{
    private readonly IMongoDatabase _database;
    private readonly SourceConfig _config;

    public MongoDbConnector(SourceConfig config)
    {
        _config = config;
        var mongoUrl = MongoUrl.Create(config.ConnectionString);
        var client = new MongoClient(mongoUrl);
        _database = client.GetDatabase(mongoUrl.DatabaseName);
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<TableInfo>> GetTablesAsync()
    {
        var collections = await _database.ListCollectionNamesAsync();
        var names = await collections.ToListAsync();
        return names.Select(n => new TableInfo { Name = n }).ToList();
    }

    public async Task<List<FieldInfo>> GetSchemaAsync(string table)
    {
        var collection = _database.GetCollection<BsonDocument>(table);
        var first = await collection.Find(new BsonDocument()).Limit(1).FirstOrDefaultAsync();
        if (first == null) return new List<FieldInfo>();

        return first.Elements.Select(e => new FieldInfo
        {
            Name = e.Name,
            Type = e.Value.BsonType.ToString(),
            Nullable = true
        }).ToList();
    }

    public async IAsyncEnumerable<DataTable> ReadChunksAsync(string query, int chunkSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<BsonDocument>(_config.Table);
        var filter = string.IsNullOrWhiteSpace(query)
            ? new BsonDocument()
            : BsonDocument.Parse(query);

        using var cursor = await collection.FindAsync(filter, cancellationToken: cancellationToken);

        DataTable? chunk = null;
        HashSet<string>? knownColumns = null;

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var doc in cursor.Current)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (chunk == null)
                {
                    chunk = new DataTable();
                    knownColumns = new HashSet<string>();
                    foreach (var el in doc.Elements)
                    {
                        chunk.Columns.Add(el.Name, typeof(string));
                        knownColumns.Add(el.Name);
                    }
                }
                else
                {
                    foreach (var el in doc.Elements)
                    {
                        if (!knownColumns!.Contains(el.Name))
                        {
                            chunk.Columns.Add(el.Name, typeof(string));
                            knownColumns.Add(el.Name);
                        }
                    }
                }

                var row = chunk.NewRow();
                foreach (var el in doc.Elements)
                    row[el.Name] = el.Value.ToString() ?? DBNull.Value.ToString();
                chunk.Rows.Add(row);

                if (chunk.Rows.Count >= chunkSize)
                {
                    yield return chunk;
                    chunk = null;
                    knownColumns = null;
                }
            }
        }

        if (chunk != null && chunk.Rows.Count > 0)
            yield return chunk;
    }

    public async Task WriteAsync(string table, DataTable data, CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<BsonDocument>(table);
        var docs = new List<BsonDocument>();
        foreach (DataRow row in data.Rows)
        {
            var doc = new BsonDocument();
            foreach (DataColumn col in data.Columns)
                doc[col.ColumnName] = row[col]?.ToString() ?? BsonNull.Value.ToString();
            docs.Add(doc);
        }
        if (docs.Count > 0)
            await collection.InsertManyAsync(docs, cancellationToken: cancellationToken);
    }
}
