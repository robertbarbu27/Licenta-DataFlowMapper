using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;
using DataFlowMapper.Core.Results;

namespace DataFlowMapper.Executor;

public class PipelineValidator
{
    private readonly IConnectorFactory _connectorFactory;

    public PipelineValidator(IConnectorFactory connectorFactory)
    {
        _connectorFactory = connectorFactory;
    }

    public async Task<List<ValidationError>> ValidateAsync(
        Pipeline pipeline,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        // 1. Circular dependencies in transform graph
        CheckCircularDependencies(pipeline.Transforms, errors);

        // 2. Fetch source schemas (connection + column existence checks)
        var allColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in pipeline.Sources)
        {
            List<FieldInfo> fields;
            try
            {
                var connector = _connectorFactory.Create(source);
                fields = await connector.GetSchemaAsync(source.Table);
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError(source.Id, "source",
                    $"Cannot connect to source '{source.Table}': {ex.Message}"));
                continue;
            }

            foreach (var f in fields)
                allColumns.Add(f.Name);
        }

        // 3. Transform input columns must exist in source schema
        foreach (var transform in pipeline.Transforms)
        {
            foreach (var input in transform.Inputs.Where(c => !string.IsNullOrEmpty(c)))
            {
                if (!allColumns.Contains(input))
                    errors.Add(new ValidationError(transform.Id, "transform",
                        $"Column '{input}' referenced in transform does not exist in any source"));
            }
        }

        // 4. FieldMapping.From columns must exist in source schema
        foreach (var target in pipeline.Targets)
        {
            foreach (var mapping in target.Mappings)
            {
                if (!allColumns.Contains(mapping.From))
                    errors.Add(new ValidationError(target.Id, "target",
                        $"Mapping column '{mapping.From}' does not exist in any source"));
            }

            // 5. Target table existence (warning — table may be created by the write)
            var targetSource = pipeline.Sources.FirstOrDefault(s => s.Id == target.ConnectorId)
                ?? pipeline.Sources.FirstOrDefault();

            if (targetSource == null) continue;

            try
            {
                var connector = _connectorFactory.Create(targetSource with
                {
                    ConnectionString = target.ConnectionString ?? targetSource.ConnectionString,
                    Type = target.Type ?? targetSource.Type
                });

                var tables = await connector.GetTablesAsync();
                var exists = tables.Any(t =>
                    t.Name.Equals(target.Table, StringComparison.OrdinalIgnoreCase));

                if (!exists)
                    errors.Add(new ValidationError(target.Id, "target",
                        $"Table '{target.Table}' does not exist — it will be created on write",
                        ValidationSeverity.Warning));
            }
            catch { /* connection errors already reported above */ }
        }

        return errors;
    }

    private static void CheckCircularDependencies(
        List<TransformDefinition> transforms,
        List<ValidationError> errors)
    {
        var stages = ExecutionGraph.BuildTransformStages(transforms);
        var resolved = stages.SelectMany(s => s).Select(t => t.Id).ToHashSet();

        foreach (var t in transforms.Where(t => !resolved.Contains(t.Id)))
            errors.Add(new ValidationError(t.Id, "transform",
                "Circular dependency detected — this transform will never execute"));
    }
}
