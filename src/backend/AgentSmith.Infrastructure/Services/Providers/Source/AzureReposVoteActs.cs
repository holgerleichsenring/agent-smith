using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-e8b9c: Azure DevOps' standing "Wait for author" votes. A vote leaves a system thread
/// (<see cref="AzureReposVoteThreads"/>); only the voter's NEWEST -5 thread counts, and only while the
/// reviewer list still shows that voter at -5 — a reset vote, or our auto-complete's +10, leaves no
/// standing act. The pull request's creator is skipped. 2026-10-13-9a59b: the act carries the vote
/// comment's host id.
/// </summary>
public static class AzureReposVoteActs
{
    private const short WaitForAuthor = -5;

    public static IReadOnlyList<PrReviewNote> Standing(
        IEnumerable<GitPullRequestCommentThread> threads, IEnumerable<IdentityRefWithVote> reviewers,
        string? creatorId, string repoUrl, string repositoryId, string projectId)
    {
        var all = threads.ToList();
        return [.. reviewers.Where(r => r.Vote == WaitForAuthor && r.Id is not null
                && !string.Equals(r.Id, creatorId, StringComparison.OrdinalIgnoreCase))
            .Select(r => AzureReposVoteThreads.Newest(all, r.Id!, WaitForAuthor))
            .OfType<(GitPullRequestCommentThread Thread, Comment Comment)>()
            .Select(v => Note(v.Thread, v.Comment, repoUrl, repositoryId, projectId))];
    }

    private static PrReviewNote Note(
        GitPullRequestCommentThread thread, Comment comment, string repoUrl, string repositoryId, string projectId) =>
        new(new PrCommentAuthor(repoUrl, repositoryId, comment.Author.Id, comment.Author.UniqueName ?? comment.Author.Id) { ProjectId = projectId },
            false, false, new DateTimeOffset(DateTime.SpecifyKind(thread.PublishedDate, DateTimeKind.Utc)), string.Empty)
        { HostId = AzureReposReviewMapping.CommentHostId(thread, comment) };
}
