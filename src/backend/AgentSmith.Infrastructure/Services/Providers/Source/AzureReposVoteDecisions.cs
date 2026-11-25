using System.Globalization;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-13-9a59b: an Azure DevOps pull request's state and standing votes — 10 approved, 5
/// approved with suggestions, -5 changes requested (wait for author), -10 rejected; a reset vote (0)
/// decides nothing. Each vote is timed and identified by its newest vote thread; one with no vote
/// thread gets "vote:{reviewerId}" and the pull request's creation time. The creator — whose
/// identity our auto-complete approves with — and the token's own identity are excluded, except at
/// -5: we never vote -5, so their Wait for author is a person's and counts.
/// </summary>
public static class AzureReposVoteDecisions
{
    /// <param name="targetTip">2026-11-24-9dc0c: the target branch's commit now, when it was read; a clean merge status
    /// computed against another target commit is stale and reads as unknown.</param>
    public static PrReviewState Read(
        GitPullRequest pr, IReadOnlyList<GitPullRequestCommentThread> threads, string? selfId, string repoUrl, string? targetTip = null)
    {
        var created = Utc(pr.CreationDate);
        var decisions = (pr.Reviewers ?? []).Where(r => r.Id is not null && Word(r.Vote) is not null
                && (r.Vote == -5 || (!Same(r.Id, pr.CreatedBy?.Id) && !Same(r.Id, selfId))))
            .Select(r => Decision(r, threads, created, repoUrl, pr))
            .ToList();
        var (state, at) = pr.Status switch
        {
            PullRequestStatus.Completed => (PrReviewStates.Merged, Utc(pr.ClosedDate)),
            PullRequestStatus.Abandoned => (PrReviewStates.Closed, Utc(pr.ClosedDate)),
            _ => (PrReviewStates.Open, created),
        };
        // 2026-11-23-8df6a: on an active pull request LastMergeCommit is the test merge, so only a completed one names it.
        return new PrReviewState(pr.PullRequestId.ToString(CultureInfo.InvariantCulture), state, at, decisions,
            pr.LastMergeSourceCommit?.CommitId, state == PrReviewStates.Merged ? pr.LastMergeCommit?.CommitId : null)
            {
                IsDraft = pr.IsDraft == true, HasConflicts = Conflicts(pr.MergeStatus, pr.LastMergeTargetCommit?.CommitId, targetTip), // 8d16, 6811a, 9dc0c
                AutoCompleteArmed = state == PrReviewStates.Open ? pr.AutoCompleteSetBy is not null : null, // 2026-11-24-629ab
            };
    }

    private static PrReviewDecision Decision(
        IdentityRefWithVote reviewer, IReadOnlyList<GitPullRequestCommentThread> threads,
        DateTimeOffset created, string repoUrl, GitPullRequest pr)
    {
        var author = new PrCommentAuthor(repoUrl, pr.Repository?.Id.ToString() ?? string.Empty, reviewer.Id, reviewer.UniqueName ?? reviewer.Id)
            { ProjectId = pr.Repository?.ProjectReference?.Id.ToString() };
        return AzureReposVoteThreads.Newest(threads, reviewer.Id, reviewer.Vote) is { } vote
            ? new PrReviewDecision(AzureReposReviewMapping.CommentHostId(vote.Thread, vote.Comment), author, false,
                Word(reviewer.Vote)!, Utc(vote.Thread.PublishedDate), vote.Comment.Content ?? string.Empty)
            : new PrReviewDecision($"vote:{reviewer.Id}", author, false, Word(reviewer.Vote)!, created, string.Empty);
    }

    private static string? Word(short vote) => vote switch
    {
        10 => PrReviewDecisions.Approved,
        5 => PrReviewDecisions.ApprovedWithSuggestions,
        -5 => PrReviewDecisions.ChangesRequested,
        -10 => PrReviewDecisions.Rejected,
        _ => null,
    };

    // 2026-11-24-6811a: queued or not set means the host has not computed it yet; a failure or policy refusal is no conflict.
    // 2026-11-24-9dc0c: Azure merges again only when the source moves, so a clean merge computed against an older target
    // commit says nothing about the target now; a conflict with an older target almost always still holds, and stays.
    internal static bool? Conflicts(PullRequestAsyncStatus status, string? mergedTarget = null, string? targetTip = null) => status switch
    {
        PullRequestAsyncStatus.Conflicts => true,
        PullRequestAsyncStatus.Succeeded when Stale(mergedTarget, targetTip) => null,
        PullRequestAsyncStatus.Succeeded or PullRequestAsyncStatus.Failure or PullRequestAsyncStatus.RejectedByPolicy => false,
        _ => null,
    };

    private static bool Stale(string? mergedTarget, string? targetTip) =>
        targetTip is not null && !string.Equals(mergedTarget, targetTip, StringComparison.OrdinalIgnoreCase);

    private static bool Same(string? a, string? b) => a is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset Utc(DateTime at) => new(DateTime.SpecifyKind(at, DateTimeKind.Utc));
}
