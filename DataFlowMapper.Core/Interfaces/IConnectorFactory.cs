using DataFlowMapper.Core.Models;

namespace DataFlowMapper.Core.Interfaces;

public interface IConnectorFactory
{
    IConnector Create(SourceConfig config);
}
