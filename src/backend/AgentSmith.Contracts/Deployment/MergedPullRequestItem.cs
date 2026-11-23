namespace AgentSmith.Contracts.Deployment;

/// <summary>
/// 2026-11-23-8df6a: one merged pull request of a ticket's code run, as the Deployments page lists it — where it merged,
/// the commit it landed as (null when no read since the upgrade named it) and when.
/// </summary>
public sealed record MergedPullRequestItem(
    string Project,
    string TicketId,
    string? TicketTitle,
    string Repo,
    string Number,
    string Url,
    string? TargetBranch,
    string? MergeSha,
    DateTimeOffset MergedAt,
    string RunId);
