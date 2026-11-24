using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Branches;

/// <summary>
/// 2026-11-24-9dc0e: a person's Try again on a refused refresh — a refusal otherwise stands until the head or the base
/// moves. The ticket's open pull request at that URL, whose newest refresh is refused and still stands, gets the same
/// refresh-base run through <see cref="BaseRefreshStart"/>; one at work (a queued row counts) or a ticket that is not free
/// starts nothing, and the answer says why.
/// </summary>
public sealed class BaseRefreshRetry(IServiceScopeFactory scopes, BaseRefreshStart start, ISourceProviderFactory sources)
{
    public async Task<BaseRefreshStarted> TryAgainAsync(ResolvedProject project, string ticketId, string url, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var scope = scopes.CreateScope();
        var record = (await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ByTicketAsync(project.Name, ticketId, ct))
            .FirstOrDefault(r => r.Url == url && r.State == PrReviewStates.Open);
        if (record is null) return BaseRefreshStarted.Refused($"{ticketId} has no open pull request {url}.");
        var said = (await scope.ServiceProvider.GetRequiredService<PullRequestMergeSaidReader>().RefreshesAsync([url], ct))
            .GetValueOrDefault(url);
        if (said is { InFlight: true }) return BaseRefreshStarted.Refused($"A refresh of {url} is already at work (run {said.RunId}).");
        if (said is null || !said.Stands(record.HeadMovedAt))
            return BaseRefreshStarted.Refused($"No refused refresh of {url} stands; there is nothing to try again.");
        if (project.Repos.FirstOrDefault(r => r.Name == record.Repo) is not { } repo)
            return BaseRefreshStarted.Refused($"The repository {record.Repo} of {url} is not configured in {project.Name}.");
        return await start.StartAsync(scope.ServiceProvider, project, repo, sources.Create(repo), RefreshedPullRequest.Of(record), ct);
    }
}
