using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Branches;

/// <summary>
/// 2026-11-17-023fb: a merged landing on a project with merge.refresh_open_pull_requests brings its base into every
/// other open agent-smith pull request of that project on the same repository and base. The database decides inline
/// who is a sibling — open, another ticket, not closed, not leased, not queued, not waiting for an answer; a ticket with
/// a run of its own merges the base at its own checkout. The host reads (which base each pull request targets) and the
/// launches run in the background, so the landing path is never held up by a host. Series siblings on another
/// repository are not touched: their base did not move.
/// </summary>
public sealed class SiblingBaseRefresh(
    IServiceScopeFactory scopes, IConfigurationLoader configLoader, ServerContext server,
    IActiveRunLease leases, IPreviousAttemptReader attempts, ITicketClosedMarkStore closed,
    ISourceProviderFactory sources, ILogger<SiblingBaseRefresh> logger) : ITicketLandedSubscriber
{
    public async Task LandedAsync(TicketLandingFact landing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(landing);
        if (landing.Kind != TicketLandingKinds.Merged) return;
        if (!configLoader.LoadConfig(server.ConfigPath).Projects.TryGetValue(landing.Project, out var project)
            || !project.Merge.RefreshOpenPullRequests) return;
        var work = await SiblingsAsync(project, landing.TicketId, cancellationToken);
        if (work.Count > 0) _ = Task.Run(() => LaunchAllAsync(project, work));
    }

    /// <summary>
    /// Per repository the landed ticket merged into, the open pull requests of the project's other tickets there that
    /// are free to refresh: not closed, not leased, not queued, not waiting for an answer.
    /// </summary>
    public async Task<IReadOnlyList<SiblingRefresh>> SiblingsAsync(ResolvedProject project, string landedTicketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var work = new List<SiblingRefresh>();
        using var scope = scopes.CreateScope();
        var records = scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>();
        var landed = (await records.ByTicketAsync(project.Name, landedTicketId, ct)).Where(r => r.State == PrReviewStates.Merged);
        foreach (var merged in landed.DistinctBy(r => r.Repo))
        {
            if (project.Repos.FirstOrDefault(r => r.Name == merged.Repo) is not { Url: { Length: > 0 } url } repo) continue;
            var open = (await records.ByRepoKeyAsync(RepoKey.Of(url), ct))
                .Where(r => r.State == PrReviewStates.Open && r.Project == project.Name && r.TicketId != landedTicketId)
                .DistinctBy(r => r.Url).ToList();
            var free = new List<PrReviewRecordView>();
            foreach (var sibling in open) if (await FreeAsync(project, sibling.TicketId, ct)) free.Add(sibling);
            if (free.Count > 0) work.Add(new SiblingRefresh(repo, merged.Url, free));
        }
        return work;
    }

    private async Task<bool> FreeAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        if ((await closed.OfAsync(project.Tracker.Name, [key], ct)).Count > 0) return false;
        if (await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct) is not null) return false;
        if (await attempts.HasQueuedAsync(project.Name, ticketId, ct)) return false;
        return (await attempts.LatestAsync(project.Name, ticketId, null, ct))?.Status != RunStatuses.WaitingForInput;
    }

    private async Task LaunchAllAsync(ResolvedProject project, IReadOnlyList<SiblingRefresh> work)
    {
        foreach (var (repo, landedUrl, siblings) in work)
        {
            try
            {
                var host = sources.Create(repo);
                if (await host.ReadPullRequestBaseAsync(landedUrl, CancellationToken.None) is not { } moved) continue;
                foreach (var sibling in siblings)
                {
                    if (await host.ReadPullRequestBaseAsync(sibling.Url, CancellationToken.None) != moved) continue;
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<BaseRefreshLauncher>()
                        .LaunchAsync(project, repo, sibling, moved, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Refreshing the open pull requests of {Project}/{Repo} failed", project.Name, repo.Name);
            }
        }
    }
}

/// <summary>2026-11-17-023fb: one repository's landed pull request and the open siblings to refresh there.</summary>
public sealed record SiblingRefresh(RepoConnection Repo, string LandedUrl, IReadOnlyList<PrReviewRecordView> Siblings);
