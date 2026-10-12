using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-08-0781: what the rework worker reads before it starts or says anything — the newest
/// keyword comment on the ticket and the newest standing request for changes or comment command on
/// each pull request the ticket's runs opened, after the attempt's cutoff, trusted. The newer one wins.
/// </summary>
public sealed class ReworkPendingActs(
    ITicketProviderFactory tickets, ISourceProviderFactory sources, IPrReviewAuthorTrust trust) : IReworkPendingActs
{
    private readonly PrStandingAct _standing = new(trust);

    public async Task<PendingReworkAct?> NewestAsync(
        ResolvedProject project, string ticketId, PreviousAttempt since, CancellationToken cancellationToken)
    {
        var newest = await KeywordActAsync(project, ticketId, since, cancellationToken);
        foreach (var (repoName, url) in since.PullRequestUrls)
            if (project.Repos.FirstOrDefault(r => r.Name == repoName) is { } repo
                && await PullRequestActAsync(repo, url, since, cancellationToken) is { } act
                && (newest is null || act.Act.At > newest.Act.At))
                newest = act;
        return newest;
    }

    private async Task<PendingReworkAct?> KeywordActAsync(
        ResolvedProject project, string ticketId, PreviousAttempt since, CancellationToken ct)
    {
        var keyword = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type)?.CommentKeyword;
        if (string.IsNullOrWhiteSpace(keyword)) return null;
        var comments = await tickets.Create(project.Tracker).GetCommentsAsync(new TicketId(ticketId), ct);
        return ReworkActReader.Read(comments, keyword, since) is { } act ? new PendingReworkAct(act) : null;
    }

    private async Task<PendingReworkAct?> PullRequestActAsync(RepoConnection repo, string url, PreviousAttempt since, CancellationToken ct)
    {
        var provider = sources.Create(repo);
        var vote = provider is IPrReviewActReader votes ? await _standing.NewestAsync(repo.Type, votes, url, since, ct) : null;
        var command = provider is IPrReviewThreadReader threads ? await _standing.NewestCommandAsync(repo.Type, threads, url, since, ct) : null;
        return PrStandingAct.Newer(vote, command) is { } note ? new PendingReworkAct(PrStandingAct.Act(note), repo, url) : null;
    }
}
