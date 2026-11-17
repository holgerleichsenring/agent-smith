using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The comment an init posts on the ticket that started it. p0321: a no-change re-init is a
/// success outcome without a pull request, and says so instead of listing "(no changes)" bullets
/// under a "Pull requests" heading. 2026-11-17-713ea: an init completes its own pull requests, so
/// the comment no longer asks anyone to merge them.
/// </summary>
public static class InitTicketSummary
{
    public static string Compose(int repoCount, IReadOnlyList<OpenedPullRequest> opened) =>
        opened.Any(o => o.Status == OpenStatus.Opened)
            ? $"""
                ## Agent Smith - Init Complete across {repoCount} repo(s)

                ### Pull requests
                {RenderPullRequestList(opened)}

                Bootstrap files (`.agentsmith/context.yaml`, `principles.md`) generated. The run completes these pull requests itself; one a branch policy holds stays open with the reason on the run.
                """
            : $"""
                ## Agent Smith - Init Complete across {repoCount} repo(s)

                No changes — project already bootstrapped and context up to date; no pull request needed.
                """;

    private static string RenderPullRequestList(IReadOnlyList<OpenedPullRequest> opened) =>
        string.Join("\n", opened.Select(o => o.Status switch
        {
            OpenStatus.Opened => $"- **{o.RepoName}**: {o.Url}",
            OpenStatus.SkippedNoChanges => $"- **{o.RepoName}**: _(no changes)_",
            _ => $"- **{o.RepoName}**: _(open failed)_",
        }));
}
