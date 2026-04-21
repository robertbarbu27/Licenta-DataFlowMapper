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
        var statsLock = new object();

        var subgraphs = ExecutionGraph.Build(pipeline);

        await EmitLog(onLog, LogLevel.Info, "Pipeline execution started", pipeline.Name);
        await EmitLog(onLog, LogLevel.Info, $"Detected {subgraphs.Count} independent branch(es)", null);

        var branchTasks = subgraphs.Select(sg =>
            RunSubgraphAsync(sg, stats, statsLock, onLog, onProgress, cancellationToken));

        await Task.WhenAll(branchTasks);

        // Post-execution row reconciliation
        var expected = stats.RowsRead;
        var actual   = stats.RowsWritten + stats.RowsSkipped;
        if (expected != actual)
        {
            var warning = $"Row reconciliation failed: read {expected}, wrote {stats.RowsWritten}, skipped {stats.RowsSkipped} (delta {expected - actual})";
            lock (statsLock) stats.IntegrityWarnings.Add(warning);
            await EmitLog(onLog, LogLevel.Warn, warning, null);
        }

        await EmitLog(onLog, LogLevel.Ok, "Pipeline execution completed", pipeline.Name);
        return stats;
    }

    private async Task RunSubgraphAsync(
        ExecutionSubgraph subgraph,
        ExecutionStats stats,
        object statsLock,
        Func<object, Task>? onLog,
        Func<ExecutionStats, Task>? onProgress,
        CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<(SourceConfig source, DataTable chunk)>(
            new BoundedChannelOptions(4)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = false
            });

        // Read all sources in this branch concurrently
        var readTask = Task.Run(async () =>
        {
            try
            {
                var sourceTasks = subgraph.Sources.Select(async source =>
                {
                    var connector = _connectorFactory.Create(source);
                    var query = source.Query ?? $"SELECT * FROM {source.Table}";
                    await EmitLog(onLog, LogLevel.Info, $"Reading from source: {source.Id}", source.Table);

                    await foreach (var chunk in connector.ReadChunksAsync(query, 1000, cancellationToken))
                    {
                        lock (statsLock) stats.RowsRead += chunk.Rows.Count;
                        await channel.Writer.WriteAsync((source, chunk), cancellationToken);
                    }
                });

                await Task.WhenAll(sourceTasks);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        // Process chunks: transform → write
        var processTask = Task.Run(async () =>
        {
            await foreach (var (source, chunk) in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var rowsIn   = chunk.Rows.Count;
                var processed = chunk;

                foreach (var stage in subgraph.TransformStages)
                    processed = ExecutionGraph.ApplyStage(processed, stage, _transformFactory, cancellationToken);

                // Per-chunk balance: rows dropped by Filter transforms count as skipped
                var rowsOut     = processed.Rows.Count;
                var chunkSkipped = rowsIn - rowsOut;
                if (chunkSkipped > 0)
                    lock (statsLock) stats.RowsSkipped += chunkSkipped;

                var writeTasks = subgraph.Targets.Select(async target =>
                {
                    var targetSource = subgraph.Sources.FirstOrDefault(s => s.Id == target.ConnectorId)
                        ?? subgraph.Sources.First();

                    var targetConnector = _connectorFactory.Create(targetSource with
                    {
                        ConnectionString = target.ConnectionString ?? targetSource.ConnectionString,
                        Type = target.Type ?? targetSource.Type
                    });

                    var mapped = ApplyMappings(processed, target.Mappings);
                    await targetConnector.WriteAsync(target.Table, mapped, cancellationToken);

                    lock (statsLock) stats.RowsWritten += mapped.Rows.Count;
                    await EmitLog(onLog, LogLevel.Ok, $"Written {mapped.Rows.Count} rows to {target.Table}", target.Id);
                });

                await Task.WhenAll(writeTasks);

                lock (statsLock) stats.ChunksDone++;

                if (onProgress != null)
                    await onProgress(stats);
            }
        }, cancellationToken);

        await Task.WhenAll(readTask, processTask);
    }

    /// <summary>
    /// Applies all transforms in a stage in parallel, each on a DataTable copy,
    /// then merges their column changes back into a single result.
    ///
    /// Fixes the previous LastOrDefault() bug where only the last transform's
    /// output survived — now all column additions and updates are preserved.
    /// </summary>
    private DataTable ApplyStage(
        DataTable input,
        List<TransformDefinition> stage,
        CancellationToken cancellationToken)
    {
        if (stage.Count == 1)
            return _transformFactory.Get(stage[0].Type).Apply(input, stage[0]);

        var tasks = stage
            .Select(t => Task.Run(
                () => (def: t, result: _transformFactory.Get(t.Type).Apply(input.Copy(), t)),
                cancellationToken))
            .ToArray();

        Task.WaitAll(tasks, cancellationToken);

        var merged = input.Copy();

        foreach (var task in tasks)
        {
            var result = task.Result.result;

            foreach (DataColumn col in result.Columns)
            {
                if (!merged.Columns.Contains(col.ColumnName))
                    merged.Columns.Add(col.ColumnName, col.DataType);

                for (var i = 0; i < merged.Rows.Count && i < result.Rows.Count; i++)
                    merged.Rows[i][col.ColumnName] = result.Rows[i][col.ColumnName];
            }
        }

        return merged;
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
