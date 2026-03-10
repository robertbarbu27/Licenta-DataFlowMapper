using System.Data;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;

namespace DataFlowMapper.Transforms;

public class FilterTransform : ITransform
{
    public string Name => "filter";

    public DataTable Apply(DataTable data, TransformDefinition config)
    {
        if (!config.Params.TryGetValue("condition", out var condition))
            return data;

        var filtered = data.Clone();
        foreach (DataRow row in data.Select(condition))
            filtered.ImportRow(row);

        return filtered;
    }
}
