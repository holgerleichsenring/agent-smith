namespace AgentSmith.Contracts.Models;

/// <summary>
/// Outcome of an atomic lifecycle-status transition attempt.
/// </summary>
public enum TransitionOutcome
{
    Succeeded,
    PreconditionFailed,
    NotFound,
    Failed
}
