namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-23-d60d: a ticket's open pull requests as its hold reads them — their CI on the heads the records hold (one of
/// <c>CiStates</c>: red wins, then pending, then green, then none; null before any CI read) and since when that state holds,
/// a red head's failed checks, the tickets it requires that have not landed (it is stacked behind their pull requests), and
/// the one open pull request's URL (null when there are several).
/// </summary>
public sealed record TicketOpenPullRequests(string? Ci, DateTimeOffset? CiSince)
{
    public IReadOnlyList<string> FailedChecks { get; init; } = [];

    public IReadOnlyList<string> Unlanded { get; init; } = [];

    public string? Url { get; init; }

    /// <summary>2026-11-24-6811a: the host said last that one of them conflicts with its base, and since when.</summary>
    public bool Conflicts { get; init; }

    public DateTimeOffset? ConflictsSince { get; init; }

    /// <summary>2026-11-24-6811d: a refresh-base run at work on one of them, or the newest one's refusal still standing (its
    /// head has not moved since); null when neither.</summary>
    public AgentSmith.Contracts.Runs.PullRequestRefreshSaid? Refresh { get; init; }

    /// <summary>2026-11-26-5201f: the URLs of the open ones whose host still needs a person's approval; empty when none.</summary>
    public IReadOnlyList<string> AwaitingApproval { get; init; } = [];

    /// <summary>2026-11-26-5201f: the URLs of the open ones that are still drafts; empty when none.</summary>
    public IReadOnlyList<string> Drafts { get; init; } = [];
}
