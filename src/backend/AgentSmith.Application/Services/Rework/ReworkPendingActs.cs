using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-08-0781: what the rework worker reads before it starts or says anything — the newest
/// keyword comment on the ticket and the newest standing request for changes or comment command on
/// each pull request the ticket's runs opened, after the attempt's cutoff, trusted. The newer one wins.
/// <para>2026-10-13-9a59m: beside them, the ticket's stored approval when it is an operator act
/// (<see cref="OperatorAct"/>), read by the tracker connection and the ticket key a run resolves.</para>
/// <para>2026-10-13-9a59n: and the attempt's own stored Retry (<see cref="RetryAct"/>), newest-wins with the rest.</para>
/// </summary>
public sealed class ReworkPendingActs(
    ITicketProviderFactory tickets, ISourceProviderFactory sources, IPrReviewAuthorTrust trust,
    ISpecApprovalStore approvals) : IReworkPendingActs
{
    private readonly PrStandingAct _standing = new(trust);

    public async Task<PendingReworkAct?> NewestAsync(
        ResolvedProject project, string ticketId, PreviousAttempt since, CancellationToken cancellationToken) =>
        Newer(Newer(await KeywordActAsync(project, ticketId, since, cancellationToken),
            await NewestHandbackAnswerAsync(project, ticketId, since, cancellationToken)),
            RetryAct.Of(since) is { } retry ? new PendingReworkAct(retry) : null);

    public async Task<PendingReworkAct?> NewestHandbackAnswerAsync(
        ResolvedProject project, string ticketId, PreviousAttempt since, CancellationToken cancellationToken)
    {
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var newest = OperatorAct.Of(await approvals.GetAsync(project.Tracker.Name, key, cancellationToken), since) is { } op
            ? new PendingReworkAct(op) : null;
        foreach (var (repoName, url) in since.PullRequestUrls)
            if (project.Repos.FirstOrDefault(r => r.Name == repoName) is { } repo)
                newest = Newer(newest, await PullRequestActAsync(repo, url, since, cancellationToken));
        return newest;
    }

    private static PendingReworkAct? Newer(PendingReworkAct? kept, PendingReworkAct? other) =>
        other is not null && (kept is null || other.Act.At > kept.Act.At) ? other : kept;

    private async Task<PendingReworkAct?> KeywordActAsync(
        ResolvedProject project, string ticketId, PreviousAttempt since, CancellationToken ct)
    {
        var keyword = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type)?.CommentKeyword;
        if (string.IsNullOrWhiteSpace(keyword)) return null;
        var comments = await tickets.Create(project.Tracker).GetCommentsAsync(new TicketId(ticketId), ct);
        return ReworkActReader.ReadComment(comments, keyword, since) is { } comment
            ? new PendingReworkAct(ReworkActReader.ActOf(comment), Text: comment.Body) : null;
    }

    private async Task<PendingReworkAct?> PullRequestActAsync(RepoConnection repo, string url, PreviousAttempt since, CancellationToken ct)
    {
        var provider = sources.Create(repo);
        var vote = provider is IPrReviewActReader votes ? await _standing.NewestAsync(repo.Type, votes, url, since, ct) : null;
        var command = provider is IPrReviewThreadReader threads ? await _standing.NewestCommandAsync(repo.Type, threads, url, since, ct) : null;
        var text = PrStandingAct.Newer(vote, command)?.Body;
        return PrStandingAct.NewerAct(vote, command, url) is { } act
            ? new PendingReworkAct(act, repo, url, string.IsNullOrWhiteSpace(text) ? null : text) : null;
    }
}
