namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-9a39c: the stacked dependent whose pull requests move, and each move.</summary>
public sealed record RetargetPullRequestsRequest(string TicketId, IReadOnlyList<PullRequestMove> Moves);
