namespace DataFlowMapper.Core.Results;

public record ValidationError(
    string NodeId,
    string NodeKind,   // "source" | "transform" | "target"
    string Message,
    ValidationSeverity Severity = ValidationSeverity.Error
);

public enum ValidationSeverity { Error, Warning }
