namespace AgentSmith.Contracts.Models;

/// <summary>
/// Result of validating a generated .context.yaml against the CCS schema.
/// </summary>
public sealed record ContextValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static ContextValidationResult Success() =>
        new(true, Array.Empty<string>());

    public static ContextValidationResult Failure(IReadOnlyList<string> errors) =>
        new(false, errors);
}
