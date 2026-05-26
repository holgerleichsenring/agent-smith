namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Discriminator for <see cref="RetryOutcome"/>.
/// </summary>
public enum RetryOutcomeKind
{
    Ok,
    ParseFailedAfterRetry,
    ValidationFailedAfterRetry
}
