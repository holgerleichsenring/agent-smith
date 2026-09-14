namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-13-a72a: whether a ticket may be claimed yet, given the slices it follows.
/// <see cref="Reason"/> is the human sentence naming the predecessor and its status — it
/// becomes the wait reason on the claim result, so the tracker says why nothing happened.
/// </summary>
public sealed record PredecessorVerdict(bool Blocked, string? Reason)
{
    public static PredecessorVerdict Ready() => new(Blocked: false, Reason: null);

    public static PredecessorVerdict Wait(string reason) => new(Blocked: true, reason);
}
