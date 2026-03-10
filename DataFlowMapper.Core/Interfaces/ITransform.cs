using System.Data;
using DataFlowMapper.Core.Models;

namespace DataFlowMapper.Core.Interfaces;

public interface ITransform
{
    string Name { get; }
    DataTable Apply(DataTable data, TransformDefinition config);
}
