using System.Data;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;

namespace DataFlowMapper.Executor;

/// <summary>
/// Partitions a pipeline into independent subgraphs using Union-Find on
/// (source, target) pairs connected via target.ConnectorId.
/// Each subgraph runs as a separate parallel Task in PipelineRunner.
/// </summary>
public static class ExecutionGraph
{
    public static List<ExecutionSubgraph> Build(Pipeline pipeline)
    {
        var parent = new Dictionary<string, string>();

        foreach (var s in pipeline.Sources) parent[s.Id] = s.Id;
        foreach (var t in pipeline.Targets) parent[t.Id] = t.Id;

        string Find(string id)
        {
            if (parent[id] != id) parent[id] = Find(parent[id]);
            return parent[id];
        }

        void Union(string a, string b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb) parent[ra] = rb;
        }

        // Connect each target to the source it references
        foreach (var target in pipeline.Targets)
        {
            var source = pipeline.Sources.FirstOrDefault(s => s.Id == target.ConnectorId);
            if (source != null)
                Union(source.Id, target.Id);
        }

        // Group sources and targets by their component root
        var sourcesPerComponent = new Dictionary<string, List<SourceConfig>>();
        var targetsPerComponent = new Dictionary<string, List<TargetConfig>>();

        foreach (var source in pipeline.Sources)
        {
            var root = Find(source.Id);
            if (!sourcesPerComponent.ContainsKey(root)) sourcesPerComponent[root] = new();
            sourcesPerComponent[root].Add(source);
        }

        foreach (var target in pipeline.Targets)
        {
            var root = Find(target.Id);
            if (!targetsPerComponent.ContainsKey(root)) targetsPerComponent[root] = new();
            targetsPerComponent[root].Add(target);
        }

        // Build transform stages once — shared structure, applied per subgraph independently
        var stages = BuildTransformStages(pipeline.Transforms);

        var roots = sourcesPerComponent.Keys.Union(targetsPerComponent.Keys);

        return roots
            .Select(root => new ExecutionSubgraph(
                sourcesPerComponent.GetValueOrDefault(root, new()),
                stages,
                targetsPerComponent.GetValueOrDefault(root, new())
            ))
            .Where(sg => sg.Sources.Count > 0 && sg.Targets.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Kahn's BFS topological sort grouped into levels.
    /// All transforms in the same level have no dependencies on each other
    /// and can be applied in parallel on separate column groups.
    ///
    ///   Level 0: [Trim, Rename]   no DependsOn
    ///   Level 1: [Concat]         DependsOn: Trim
    ///   Level 2: [Filter]         DependsOn: Concat
    /// </summary>
    public static List<List<TransformDefinition>> BuildTransformStages(
        List<TransformDefinition> transforms)
    {
        if (transforms.Count == 0) return new();

        var inDegree  = transforms.ToDictionary(t => t.Id, _ => 0);
        var dependents = transforms.ToDictionary(t => t.Id, _ => new List<string>());

        foreach (var t in transforms)
            foreach (var dep in t.DependsOn.Where(d => inDegree.ContainsKey(d)))
            {
                inDegree[t.Id]++;
                dependents[dep].Add(t.Id);
            }

        var queue  = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var lookup = transforms.ToDictionary(t => t.Id);
        var stages = new List<List<TransformDefinition>>();

        while (queue.Count > 0)
        {
            var levelSize = queue.Count;
            var stage = new List<TransformDefinition>();

            for (var i = 0; i < levelSize; i++)
            {
                var id = queue.Dequeue();
                stage.Add(lookup[id]);
                foreach (var dep in dependents[id])
                    if (--inDegree[dep] == 0)
                        queue.Enqueue(dep);
            }

            stages.Add(stage);
        }

        return stages;
    }

    /// <summary>
    /// Applies all transforms in a stage in parallel, each on a DataTable copy,
    /// then merges their column changes back into a single result.
    ///
    /// Fixes the previous LastOrDefault() bug where only the last transform's
    /// output survived — now all column additions and updates are preserved.
    /// </summary>
    public static DataTable ApplyStage(
        DataTable input,
        List<TransformDefinition> stage,
        ITransformFactory transformFactory,
        CancellationToken cancellationToken = default)
    {
        if (stage.Count == 1)
            return transformFactory.Get(stage[0].Type).Apply(input, stage[0]);

        var tasks = stage
            .Select(t => Task.Run(
                () => (def: t, result: transformFactory.Get(t.Type).Apply(input.Copy(), t)),
                cancellationToken))
            .ToArray();

        Task.WaitAll(tasks, cancellationToken);

        var merged = input.Copy();

        foreach (var task in tasks)
        {
            var (def, result) = task.Result;

            // Only merge columns this transform declared — prevents a transform's
            // unmodified copy of an unrelated column from overwriting another
            // transform's result (e.g. Rename's copy of col_a clobbering Trim's output).
            var owned = def.Inputs
                .Concat(def.Outputs)
                .Append(def.Output ?? string.Empty)
                .Where(c => !string.IsNullOrEmpty(c))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (DataColumn col in result.Columns)
            {
                if (owned.Count > 0 && !owned.Contains(col.ColumnName))
                    continue;

                if (!merged.Columns.Contains(col.ColumnName))
                    merged.Columns.Add(col.ColumnName, col.DataType);

                for (var i = 0; i < merged.Rows.Count && i < result.Rows.Count; i++)
                    merged.Rows[i][col.ColumnName] = result.Rows[i][col.ColumnName];
            }
        }

        return merged;
    }
}

public record ExecutionSubgraph(
    List<SourceConfig>                  Sources,
    List<List<TransformDefinition>>     TransformStages,
    List<TargetConfig>                  Targets
);
