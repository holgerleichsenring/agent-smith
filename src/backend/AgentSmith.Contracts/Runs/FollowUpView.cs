namespace AgentSmith.Contracts.Runs;

/// <summary>2026-11-18-c8e2d: a follow-up spec a master proposed — a one-line goal, why it was left out of its spec,
/// and the spec it came from (null when the view does not know).</summary>
public sealed record FollowUpView(string Goal, string Why, string? SpecId = null);
