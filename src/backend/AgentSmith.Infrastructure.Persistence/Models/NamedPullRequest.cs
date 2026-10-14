namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-10-13-9a59b: a pull request a ticket's code runs named, as Runs.PullRequestsJson stores it —
/// with the newest run that names it and when it was first recorded as opened.
/// </summary>
public sealed record NamedPullRequest(
    string Project, string TicketId, string Url, string Repo, string RunId, DateTimeOffset OpenedAt);
