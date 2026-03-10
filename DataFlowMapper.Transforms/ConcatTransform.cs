using System.Data;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;

namespace DataFlowMapper.Transforms;

public class ConcatTransform : ITransform
{
    public string Name => "concat";

    public DataTable Apply(DataTable data, TransformDefinition config)
    {
        var separator = config.Params.TryGetValue("separator", out var sep) ? sep : "";
        var outputCol = config.Output ?? "concat_result";

        if (!data.Columns.Contains(outputCol))
            data.Columns.Add(outputCol, typeof(string));

        foreach (DataRow row in data.Rows)
        {
            var parts = config.Inputs
                .Where(data.Columns.Contains)
                .Select(col => row[col]?.ToString() ?? "");
            row[outputCol] = string.Join(separator, parts);
        }

        return data;
    }
}
