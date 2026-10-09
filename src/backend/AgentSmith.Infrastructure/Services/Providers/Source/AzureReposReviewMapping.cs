using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-e8b9d: Azure DevOps threads as review threads. Deleted threads and soft-deleted
/// comments are skipped; Active and Pending are unresolved, the closing statuses resolved, Unknown
/// (the shape of system threads) not resolvable. Azure DevOps marks no bot identity — its System
/// comments are the only machine-authored notes it names. 2026-10-08-f147: a marked comment is ours
/// only when the token's identity (<paramref name="selfId"/>) wrote it.
/// </summary>
public static class AzureReposReviewMapping
{
    public static IReadOnlyList<PrReviewThread> Map(
        IEnumerable<GitPullRequestCommentThread> threads, string repoUrl, string repositoryId, string projectId, string? selfId) =>
        [.. threads.Where(t => t.IsDeleted != true).Select(t => new PrReviewThread(
            t.ThreadContext?.FilePath, t.ThreadContext?.RightFileStart?.Line, Resolved(t.Status),
            [.. (t.Comments ?? []).Where(c => c.IsDeleted != true).Select(c => Note(c, repoUrl, repositoryId, projectId, selfId))]))];

    private static PrReviewNote Note(Comment comment, string repoUrl, string repositoryId, string projectId, string? selfId) =>
        new(comment.Author is { Id: { } id } author
                ? new PrCommentAuthor(repoUrl, repositoryId, id, author.UniqueName ?? id) { ProjectId = projectId }
                : null,
            IsBot: false, IsSystem: comment.CommentType == CommentType.System,
            new DateTimeOffset(DateTime.SpecifyKind(comment.PublishedDate, DateTimeKind.Utc)), comment.Content ?? string.Empty)
        {
            IsOurs = OwnPrNoteMarker.IsOurs(comment.Content,
                selfId is not null && string.Equals(comment.Author?.Id, selfId, StringComparison.OrdinalIgnoreCase)),
        };

    private static bool? Resolved(CommentThreadStatus status) => status switch
    {
        CommentThreadStatus.Active or CommentThreadStatus.Pending => false,
        CommentThreadStatus.Unknown => null,
        _ => true,
    };
}
