namespace AgentSmith.Contracts.Models;

/// <summary>
/// What a run's record says about how it stands: its status, whether that status is an ending
/// (a waiting run is not ended), the summary it closed with — the failure reason when it
/// failed — and the pull requests it opened.
/// </summary>
public sealed record RunOutcome(
    string Status,
    bool IsEnded,
    string? Summary,
    IReadOnlyList<string> PullRequestUrls);
