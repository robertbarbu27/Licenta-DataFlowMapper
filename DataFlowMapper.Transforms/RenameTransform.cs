using System.Data;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;

namespace DataFlowMapper.Transforms;

public class RenameTransform : ITransform
{
    public string Name => "rename";

    public DataTable Apply(DataTable data, TransformDefinition config)
    {
        foreach (var (from, to) in config.Params)
        {
            if (data.Columns.Contains(from))
                data.Columns[from]!.ColumnName = to;
        }
        return data;
    }
}
