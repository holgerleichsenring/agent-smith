namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Result of <see cref="RetryCoordinator.InvokeAsync"/>: the final output (when one
/// was produced), the outcome kind, and the structured failure reason if persistent
/// after the single retry.
/// </summary>
public sealed record RetryOutcome
{
    public required RetryOutcomeKind Kind { get; init; }
    public string? FinalOutput { get; init; }
    public string? FailureReason { get; init; }

    public static RetryOutcome Ok(string output)
        => new() { Kind = RetryOutcomeKind.Ok, FinalOutput = output };

    public static RetryOutcome ParseFailed(string reason, string? lastOutput)
        => new()
        {
            Kind = RetryOutcomeKind.ParseFailedAfterRetry,
            FailureReason = reason,
            FinalOutput = lastOutput
        };

    public static RetryOutcome ValidationFailed(string reason, string? lastOutput)
        => new()
        {
            Kind = RetryOutcomeKind.ValidationFailedAfterRetry,
            FailureReason = reason,
            FinalOutput = lastOutput
        };
}
