namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Input to <see cref="OutcomeClassifier.Classify"/>. Pure data carrier — every
/// field is set by the runtime after a chat invocation completes. <see cref="LimitHit"/>
/// is null when no limit fired, otherwise the LimitDecision raised by LimitEnforcer.
/// </summary>
public sealed record ClassificationInput
{
    public required bool ResponsePresent { get; init; }
    public required bool ParseSuccess { get; init; }
    public required bool ValidationSuccess { get; init; }
    public Exception? CaughtException { get; init; }
    public LimitDecision? LimitHit { get; init; }
}
