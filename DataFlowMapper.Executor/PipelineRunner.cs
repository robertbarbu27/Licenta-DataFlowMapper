using System.Data;
using System.Threading.Channels;
using DataFlowMapper.Core.Enums;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;
using DataFlowMapper.Core.Results;

namespace DataFlowMapper.Executor;

public class PipelineRunner
{
    private readonly IConnectorFactory _connectorFactory;
    private readonly ITransformFactory _transformFactory;

    public PipelineRunner(IConnectorFactory connectorFactory, ITransformFactory transformFactory)
    {
        _connectorFactory = connectorFactory;
        _transformFactory = transformFactory;
    }

    public async Task<ExecutionStats> ExecuteAsync(
        Pipeline pipeline,
        Func<object, Task>? onLog,
        Func<ExecutionStats, Task>? onProgress,
        CancellationToken cancellationToken)
    {
        var stats = new ExecutionStats();
        var stages = BuildTransformStages(pipeline.Transforms);

        var channel = Channel.CreateBounded<(SourceConfig source, DataTable chunk)>(
            new BoundedChannelOptions(4)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = false
            });

        await EmitLog(onLog, LogLevel.Info, "Pipeline execution started", pipeline.Name);

        var readTask = Task.Run(async () =>
        {
            try
            {
                foreach (var source in pipeline.Sources)
                {
                    var connector = _connectorFactory.Create(source);
                    var query = source.Query ?? $"SELECT * FROM {source.Table}";
                    await EmitLog(onLog, LogLevel.Info, $"Reading from source: {source.Id}", source.Table);

                    await foreach (var chunk in connector.ReadChunksAsync(query, 1000, cancellationToken))
                    {
                        stats.RowsRead += chunk.Rows.Count;
                        await channel.Writer.WriteAsync((source, chunk), cancellationToken);
                    }
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        var processTask = Task.Run(async () =>
        {
            await foreach (var (source, chunk) in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var processed = chunk;

                foreach (var stage in stages)
                {
                    var stageTasks = stage.Select(t => Task.Run(() =>
                    {
                        var transform = _transformFactory.Get(t.Type);
                        return transform.Apply(processed.Copy(), t);
                    }, cancellationToken)).ToList();

                    var results = await Task.WhenAll(stageTasks);
                    processed = results.LastOrDefault() ?? processed;
                }

                var writeTasks = pipeline.Targets.Select(async target =>
                {
                    var targetSource = pipeline.Sources.FirstOrDefault(s => s.Id == target.ConnectorId)
                        ?? pipeline.Sources.First();
                    var targetConnector = _connectorFactory.Create(targetSource with
                    {
                        ConnectionString = targetSource.ConnectionString
                    });

                    var mapped = ApplyMappings(processed, target.Mappings);
                    await targetConnector.WriteAsync(target.Table, mapped, cancellationToken);
                    stats.RowsWritten += mapped.Rows.Count;

                    await EmitLog(onLog, LogLevel.Ok, $"Written {mapped.Rows.Count} rows to {target.Table}", target.Id);
                });

                await Task.WhenAll(writeTasks);
                stats.ChunksDone++;

                if (onProgress != null)
                    await onProgress(stats);
            }
        }, cancellationToken);

        await Task.WhenAll(readTask, processTask);
        await EmitLog(onLog, LogLevel.Ok, "Pipeline execution completed", pipeline.Name);
        return stats;
    }

    private static List<List<TransformDefinition>> BuildTransformStages(List<TransformDefinition> transforms)
    {
        var stages = new List<List<TransformDefinition>>();
        var resolved = new HashSet<string>();
        var remaining = transforms.ToList();

        while (remaining.Count > 0)
        {
            var stage = remaining
                .Where(t => t.DependsOn.All(dep => resolved.Contains(dep)))
                .ToList();

            if (stage.Count == 0) break;

            stages.Add(stage);
            foreach (var t in stage)
            {
                resolved.Add(t.Id);
                remaining.Remove(t);
            }
        }

        return stages;
    }

    private static DataTable ApplyMappings(DataTable source, List<FieldMapping> mappings)
    {
        if (mappings.Count == 0) return source;

        var result = new DataTable();
        foreach (var mapping in mappings)
            result.Columns.Add(mapping.To, typeof(string));

        foreach (DataRow row in source.Rows)
        {
            var newRow = result.NewRow();
            foreach (var mapping in mappings)
            {
                if (source.Columns.Contains(mapping.From))
                    newRow[mapping.To] = row[mapping.From];
            }
            result.Rows.Add(newRow);
        }
        return result;
    }

    private static async Task EmitLog(Func<object, Task>? onLog, LogLevel level, string message, string? meta = null)
    {
        if (onLog != null)
        {
            await onLog(new
            {
                Level = level.ToString(),
                Message = message,
                Timestamp = DateTime.UtcNow,
                Meta = meta
            });
        }
    }
}
