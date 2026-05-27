namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Output of <see cref="ISkillOutputValidator.Validate"/>. Carries success/failure
/// plus the concrete error message used to build the retry hint.
/// </summary>
public sealed record ValidationResult
{
    public required bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static ValidationResult Valid() => new() { IsValid = true };

    public static ValidationResult Invalid(string error)
        => new() { IsValid = false, ErrorMessage = error };
}
