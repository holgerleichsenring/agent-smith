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
    public static PrReviewState Read(
        GitPullRequest pr, IReadOnlyList<GitPullRequestCommentThread> threads, string? selfId, string repoUrl)
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
        return new PrReviewState(pr.PullRequestId.ToString(CultureInfo.InvariantCulture), state, at, decisions);
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

    private static bool Same(string? a, string? b) => a is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset Utc(DateTime at) => new(DateTime.SpecifyKind(at, DateTimeKind.Utc));
}
