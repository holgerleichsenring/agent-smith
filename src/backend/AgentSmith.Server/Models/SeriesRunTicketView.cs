namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: one member ticket on the series page.</summary>
/// <param name="Depth">The longest requires path to it inside the series; 0 without one.</param>
/// <param name="Requires">The ticket ids it requires, inside or outside the series.</param>
/// <param name="Repos">The repos it has pull requests in, in the project's repo order.</param>
/// <param name="Status">The newest attempt's status; null before any.</param>
public sealed record SeriesRunTicketView(
    string TicketId, string Key, string Title, bool FollowUp, int Depth, IReadOnlyList<string> Requires,
    IReadOnlyList<string> Repos, bool Landed, string? Status, IReadOnlyList<SeriesRunPullRequestView> PullRequests);
