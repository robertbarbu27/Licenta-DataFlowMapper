namespace DataFlowMapper.Core.Models;

public record SourceConfig
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string? Query { get; set; }
}
