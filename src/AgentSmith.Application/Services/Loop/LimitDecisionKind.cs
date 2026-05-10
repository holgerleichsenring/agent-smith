namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Discriminator for <see cref="LimitDecision"/>.
/// </summary>
public enum LimitDecisionKind
{
    Continue,
    CappedTokens,
    CappedTime
}
