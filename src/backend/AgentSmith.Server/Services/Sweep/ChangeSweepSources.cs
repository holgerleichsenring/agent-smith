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
/// Nudges are origin Sweep: the worker serves acts only. A RefreshOnly item — an approval, a vote
/// change — is a Refresh nudge, which only reads the pull request's record again.
/// </summary>
public sealed class ChangeSweepSources(
    ITicketProviderFactory tickets,
    ISourceProviderFactory sources,
    IConfiguredRepoFinder repoFinder,
    IPreviousAttemptReader attempts,
    IReworkNudges nudges)
{
    private readonly HashSet<string> _nudgedOnce = new(StringComparer.Ordinal);

    // 2026-11-25-cc1f: a refresh name is a pull request's state; it is nudged when it differs from the one last nudged for that
    // pull request, so a state that returns (ready, draft, ready again) is read again, and one entry per pull request is kept.
    private readonly Dictionary<string, string> _lastRefresh = new(StringComparer.Ordinal);

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
        if (item.Dedupe is not null && !Fresh(item)) return;
        var owners = new List<string>();
        foreach (var owner in repoFinder.FindAll(config, repo.Url!))
            if (await attempts.LatestAsync(owner.ProjectName, ticket, null, ct) is not null) owners.Add(owner.ProjectName);
        if (owners.Count == 1)
            await nudges.EnqueueAsync(new ReworkNudgeRequest(owners[0], ticket,
                item.RefreshOnly ? ReworkNudgeOrigin.Refresh : ReworkNudgeOrigin.Sweep, item.PrUrl), ct);
    }

    private bool Fresh(ChangedItem item)
    {
        if (!item.RefreshOnly || item.PrUrl is not { } url) return _nudgedOnce.Add(item.Dedupe!);
        if (_lastRefresh.TryGetValue(url, out var last) && last == item.Dedupe) return false;
        _lastRefresh[url] = item.Dedupe!;
        return true;
    }

    // A cursor key fits an indexed column; a long repository URL is named by its hash.
    private static string Key(string key) => key.Length <= 180
        ? key : "pr:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
