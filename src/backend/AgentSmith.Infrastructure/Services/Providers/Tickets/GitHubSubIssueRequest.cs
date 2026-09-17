namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// The REST call that makes an issue a sub-issue of another. Octokit 14 has no typed call for
/// it, so it goes through the client's connection. The body names the child by its DATABASE id,
/// not its issue number — the number addresses the parent in the path only.
/// </summary>
internal sealed record GitHubSubIssueRequest(Uri Path, IReadOnlyDictionary<string, object> Body)
{
    public const string Accepts = "application/vnd.github+json";

    public static GitHubSubIssueRequest For(
        string owner, string repo, int parentNumber, long childDatabaseId) =>
        new(new Uri($"repos/{owner}/{repo}/issues/{parentNumber}/sub_issues", UriKind.Relative),
            new Dictionary<string, object> { ["sub_issue_id"] = childDatabaseId });
}
