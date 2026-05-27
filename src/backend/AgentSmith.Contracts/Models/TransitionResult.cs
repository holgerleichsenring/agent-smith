namespace AgentSmith.Contracts.Models;

/// <summary>
/// Result of an ITicketStatusTransitioner.TransitionAsync call.
/// </summary>
public sealed record TransitionResult(TransitionOutcome Outcome, string? Error = null)
{
    public static TransitionResult Succeeded() => new(TransitionOutcome.Succeeded);
    public static TransitionResult PreconditionFailed(string reason)
        => new(TransitionOutcome.PreconditionFailed, reason);
    public static TransitionResult NotFound() => new(TransitionOutcome.NotFound);
    public static TransitionResult Failed(string error) => new(TransitionOutcome.Failed, error);

    public bool IsSuccess => Outcome == TransitionOutcome.Succeeded;
}
