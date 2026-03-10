namespace DataFlowMapper.Core.Models;

public class Pipeline
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SourceConfig> Sources { get; set; } = new();
    public List<TargetConfig> Targets { get; set; } = new();
    public List<TransformDefinition> Transforms { get; set; } = new();
    public List<JoinDefinition> Joins { get; set; } = new();
}
