using DataFlowMapper.Core.Enums;

namespace DataFlowMapper.Core.Models;

public class TargetConfig
{
    public string Id { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string ConnectorId { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
    public string? Type { get; set; }
    public WriteMode Mode { get; set; }
    public List<FieldMapping> Mappings { get; set; } = new();
}
