using DataFlowMapper.Core.Enums;

namespace DataFlowMapper.Core.Results;

public class LogMessage
{
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Meta { get; set; } = new();
}
