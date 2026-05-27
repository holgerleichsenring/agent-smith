namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Result of a <see cref="LimitEnforcer"/> recording call. Continue means within
/// limits; the two Capped kinds carry a structured reason for log/trace.
/// </summary>
public sealed record LimitDecision
{
    public required LimitDecisionKind Kind { get; init; }
    public string? Reason { get; init; }

    public bool IsContinue => Kind == LimitDecisionKind.Continue;

    public static LimitDecision Continue() => new() { Kind = LimitDecisionKind.Continue };

    public static LimitDecision Cap(LimitDecisionKind kind, string reason)
        => new() { Kind = kind, Reason = reason };
}
