using System.Data;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;

namespace DataFlowMapper.Transforms;

public class TrimTransform : ITransform
{
    public string Name => "trim";

    public DataTable Apply(DataTable data, TransformDefinition config)
    {
        var columns = config.Inputs.Where(data.Columns.Contains).ToList();

        foreach (DataRow row in data.Rows)
        {
            foreach (var col in columns)
            {
                if (row[col] is string val)
                    row[col] = val.Trim();
            }
        }

        return data;
    }
}
