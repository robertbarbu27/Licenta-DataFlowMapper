using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;

namespace DataFlowMapper.Connectors;

public class ConnectorFactory : IConnectorFactory
{
    public IConnector Create(SourceConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "postgresql" or "postgres" => new PostgreSqlConnector(config),
            "mysql" => new MySqlConnector(config),
            "mongodb" or "mongo" => new MongoDbConnector(config),
            _ => throw new NotSupportedException($"Connector type '{config.Type}' is not supported.")
        };
    }
}
