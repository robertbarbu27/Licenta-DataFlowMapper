namespace DataFlowMapper.Core.Models;

public class JoinDefinition
{
    public string Left { get; set; } = string.Empty;
    public string Right { get; set; } = string.Empty;
    public string On { get; set; } = string.Empty;
    public string JoinType { get; set; } = string.Empty;
}
