using System.Data;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;

namespace DataFlowMapper.Transforms;

public class MapValuesTransform : ITransform
{
    public string Name => "mapvalues";

    public DataTable Apply(DataTable data, TransformDefinition config)
    {
        var inputCol = config.Inputs.FirstOrDefault() ?? "";
        var outputCol = config.Output ?? inputCol;

        if (!string.IsNullOrEmpty(outputCol) && !data.Columns.Contains(outputCol))
            data.Columns.Add(outputCol, typeof(string));

        foreach (DataRow row in data.Rows)
        {
            var val = row[inputCol]?.ToString() ?? "";
            row[outputCol] = config.Params.TryGetValue(val, out var mapped) ? mapped : val;
        }

        return data;
    }
}
