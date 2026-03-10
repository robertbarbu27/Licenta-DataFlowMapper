namespace DataFlowMapper.Core.Models;

public class TransformDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> Inputs { get; set; } = new();
    public List<string> Outputs { get; set; } = new();
    public string? Output { get; set; }
    public Dictionary<string, string> Params { get; set; } = new();
    public List<string> DependsOn { get; set; } = new();
}
