namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-9a39c: one pull request of <paramref name="Repo"/> to move from <paramref name="From"/> — a landed
/// prerequisite's branch — onto <paramref name="To"/>; moved only while it is still based on From.</summary>
public sealed record PullRequestMove(string Repo, string Url, string From, string To);
