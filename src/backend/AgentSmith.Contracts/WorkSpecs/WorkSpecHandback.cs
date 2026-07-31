namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: a hand-back carried on the spec. The case code drives the routing
/// (clarification park vs. verdict park); the reason is for the human reading
/// the ticket comment and is never compared mechanically.
/// </summary>
public sealed record WorkSpecHandback(WorkSpecHandbackCase Case, string Reason)
{
    /// <summary>
    /// NotImplementable is a verdict, not a question: it parks in its own status,
    /// does not auto-retry on a comment, and restarts only on an explicit Retry.
    /// </summary>
    public bool IsVerdict => Case == WorkSpecHandbackCase.NotImplementable;
}
