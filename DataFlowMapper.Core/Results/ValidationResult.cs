namespace DataFlowMapper.Core.Results;

public class ValidationResult
{
    public bool IsValid { get; private set; } = true;
    public List<string> Errors { get; } = new();

    public void AddError(string error)
    {
        IsValid = false;
        Errors.Add(error);
    }
}
