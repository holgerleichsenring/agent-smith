namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-11-23-0219: what the newest merge-green run said about one pull request when it did not merge it — refused
/// (<see cref="PullRequestStatuses.CompletionRefused"/>, with the host's reason) or armed to complete once the host's rules
/// pass (<see cref="PullRequestStatuses.CompletionArmed"/>) — and when that run started.
/// </summary>
public sealed record PullRequestMergeSaid(string Status, string? Reason, DateTimeOffset At);
