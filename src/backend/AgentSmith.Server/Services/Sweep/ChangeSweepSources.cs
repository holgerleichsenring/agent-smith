using System.Security.Cryptography;
using System.Text;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-08-9e6e: the change sources of the polling tracker entries — each entry's tickets, and
/// every repository its projects declare, once per cycle across entries. A changed ticket with a code
/// run is nudged for its project; a changed pull request from an agent-smith branch of the same
/// repository is nudged when exactly one configured project of that repository ran the ticket.
/// Nudges are origin Sweep: the worker serves acts only.
/// </summary>
public sealed class ChangeSweepSources(
    ITicketProviderFactory tickets,
    ISourceProviderFactory sources,
    IConfiguredRepoFinder repoFinder,
    IPreviousAttemptReader attempts,
    IReworkNudges nudges)
{
    private readonly HashSet<string> _nudgedOnce = new(StringComparer.Ordinal);

    public IReadOnlyList<SweepSource> For(AgentSmithConfig config, IEnumerable<TrackerConnection> polling)
    {
        var list = new List<SweepSource>();
        var repos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tracker in polling)
        {
            var projects = config.Projects.Values.Where(p => p.Tracker.Name == tracker.Name).ToList();
            if (tickets.Create(tracker) is IChangedTicketLister lister)
                list.Add(new SweepSource($"tickets:{tracker.Name}", (since, _, pages, ct) => lister.ChangedSinceAsync(since, pages, ct),
                    (item, ct) => NudgeTicketAsync(projects, item, ct)));
            foreach (var repo in projects.SelectMany(p => p.Repos).Where(r => r.Url is not null && repos.Add(r.Url)))
                if (sources.Create(repo) is IChangedPullRequestLister prs)
                    list.Add(new SweepSource(Key("pr:" + repo.Url), prs.ChangedSinceAsync, (item, ct) => NudgePullRequestAsync(config, repo, item, ct)));
        }
        return list;
    }

    private async Task NudgeTicketAsync(IReadOnlyList<ResolvedProject> projects, ChangedItem item, CancellationToken ct)
    {
        if (item.TicketId is not { } ticket) return;
        foreach (var project in projects)
            if (await attempts.LatestAsync(project.Name, ticket, null, ct) is not null)
                await nudges.EnqueueAsync(new ReworkNudgeRequest(project.Name, ticket, ReworkNudgeOrigin.Sweep), ct);
    }

    private async Task NudgePullRequestAsync(AgentSmithConfig config, RepoConnection repo, ChangedItem item, CancellationToken ct)
    {
        if (!item.SameRepository || TicketBranchNamer.TicketOf(item.HeadRef) is not { } ticket) return;
        if (item.Dedupe is not null && !_nudgedOnce.Add(item.Dedupe)) return;
        var owners = new List<string>();
        foreach (var owner in repoFinder.FindAll(config, repo.Url!))
            if (await attempts.LatestAsync(owner.ProjectName, ticket, null, ct) is not null) owners.Add(owner.ProjectName);
        if (owners.Count == 1)
            await nudges.EnqueueAsync(new ReworkNudgeRequest(owners[0], ticket, ReworkNudgeOrigin.Sweep, item.PrUrl), ct);
    }

    // A cursor key fits an indexed column; a long repository URL is named by its hash.
    private static string Key(string key) => key.Length <= 180
        ? key : "pr:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
