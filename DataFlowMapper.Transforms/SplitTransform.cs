using System.Data;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;

namespace DataFlowMapper.Transforms;

public class SplitTransform : ITransform
{
    public string Name => "split";

    public DataTable Apply(DataTable data, TransformDefinition config)
    {
        var inputCol = config.Inputs.FirstOrDefault() ?? "";
        var delimiter = config.Params.TryGetValue("delimiter", out var d) ? d : ",";

        foreach (var outCol in config.Outputs)
        {
            if (!data.Columns.Contains(outCol))
                data.Columns.Add(outCol, typeof(string));
        }

        foreach (DataRow row in data.Rows)
        {
            var value = row[inputCol]?.ToString() ?? "";
            var parts = value.Split(delimiter);
            for (int i = 0; i < config.Outputs.Count; i++)
                row[config.Outputs[i]] = i < parts.Length ? parts[i] : string.Empty;
        }

        return data;
    }
}
