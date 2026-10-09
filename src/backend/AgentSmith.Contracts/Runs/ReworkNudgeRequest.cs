namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-08-0781: "check this ticket". It carries no act — the worker reads the act itself — only
/// where it came from, the pull request a review named, and how long to wait before checking.
/// </summary>
public sealed record ReworkNudgeRequest(
    string Project,
    string TicketId,
    ReworkNudgeOrigin Origin,
    string? PrUrl = null,
    ReworkChannel? Channel = null,
    TimeSpan? Delay = null)
{
    /// <summary>A run end and a sweep never pull a waiting nudge forward; a person's act does.</summary>
    public bool IsLate => Origin is ReworkNudgeOrigin.RunEnd or ReworkNudgeOrigin.Sweep;
}
