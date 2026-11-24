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
}
