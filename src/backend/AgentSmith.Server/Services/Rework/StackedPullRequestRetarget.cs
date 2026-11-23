using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Acts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-17-9a39c: a stacked dependent's open pull requests leave a landed prerequisite's branch. Asked on EVERY nudge
/// of a ticket with an attempt — idempotent, so a RequirementsMet origin lost to a merge, or a nudge served while a run
/// held the ticket, is caught by the run's own end. A live run is left alone: its CommitAndPR may still move the pull
/// request back onto the rung it resolved, and its end nudges again.
/// <para>The new base is the next stack tip's branch, else the base the prerequisite's own pull request in that repo
/// had (the providers read a merged pull request's base), else the repo's configured default branch. This class only
/// READS; the moves run as a 'retarget' run (<see cref="PullRequestRetargetRun"/>).</para>
/// <para>2026-11-23-97c2b: the next tip is chosen per repo — the next unlanded prerequisite with work in THAT repo — so a pull
/// request never moves onto a branch its repo does not have; and a landed prerequisite anywhere in the chain counts, since a
/// pull request stacked per repo may sit on a deeper prerequisite's branch.</para>
/// </summary>
public sealed class StackedPullRequestRetarget(
    ITicketRequirementStore requirements,
    ITicketLandingStore landings,
    GreenPrerequisites green, // 2026-11-23-97c2b: the chain and the tip per repo
    IPreviousAttemptReader attempts,
    IActiveRunLease leases,
    ISourceProviderFactory sources,
    IServiceScopeFactory scopes,
    ILogger<StackedPullRequestRetarget> logger)
{
    /// <summary>Moves what sits on a landed prerequisite's branch; answers how many moves it asked for.</summary>
    public async Task<int> RetargetAsync(ResolvedProject project, string ticketId, PreviousAttempt attempt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (attempt.PullRequestUrls.Count == 0) return 0;
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var edges = await requirements.RequiredOfAsync(project.Tracker.Name, key, ct);
        if (edges.Count == 0 || await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct) is not null) return 0;
        var reads = green.Reads(project.Name);
        var (landed, unmet) = await ChainAsync(reads, project.Tracker.Name, edges, ct);
        if (landed.Count == 0) return 0;
        var tips = unmet.Count == 0 ? new Dictionary<string, TicketRequirementEdge?>() : await reads.TipPerRepoAsync(unmet, ct);
        var moves = new List<PullRequestMove>();
        foreach (var (repo, url) in attempt.PullRequestUrls)
            if (await MoveOfAsync(project, repo, url, landed, tips.GetValueOrDefault(repo), ct) is { } move) moves.Add(move);
        if (moves.Count == 0) return 0;
        (int? Moved, string? Ended) run;
        await using (var scope = scopes.CreateAsyncScope())
            run = await scope.ServiceProvider.GetRequiredService<PullRequestRetargetRun>().RunAsync(project, ticketId, moves);
        logger.LogInformation("Ticket {Project}/{Ticket}: {Count} pull request(s) asked to leave a landed branch; moved {Moved} {Ended}",
            project.Name, ticketId, moves.Count, run.Moved, run.Ended);
        return moves.Count;
    }

    // The branch of each landed prerequisite of the ticket's chain — a pull request stacked per repo may sit on a deeper
    // one's (2026-11-23-97c2b) — keyed by the branch name a stacked pull request targets; and the unlanded direct edges.
    private async Task<(Dictionary<string, TicketRequirementEdge> Landed, IReadOnlyList<TicketRequirementEdge> Unmet)> ChainAsync(
        GreenReads reads, string tracker, IReadOnlyList<TicketRequirementEdge> edges, CancellationToken ct)
    {
        var chain = await reads.ChainAsync(edges, ct);
        var landed = (await landings.LandedAsync(tracker, [.. chain.Select(e => e.RequiredTicketKey)], ct))
            .Select(l => l.TicketKey).ToHashSet(StringComparer.Ordinal);
        return (chain.Where(e => landed.Contains(e.RequiredTicketKey))
                .ToDictionary(e => TicketBranchNamer.Compose(new TicketId(e.RequiredTicketId)).Value, StringComparer.Ordinal),
            [.. edges.Where(e => !landed.Contains(e.RequiredTicketKey))]);
    }

    private async Task<PullRequestMove?> MoveOfAsync(ResolvedProject project, string repo, string url,
        IReadOnlyDictionary<string, TicketRequirementEdge> landed, TicketRequirementEdge? tip, CancellationToken ct)
    {
        if (project.Repos.FirstOrDefault(r => r.Name == repo) is not { } connection) return null;
        var provider = sources.Create(connection);
        if (await provider.ReadPullRequestBaseAsync(url, ct) is not { } current || !landed.TryGetValue(current, out var edge)) return null;
        var to = tip is not null ? TicketBranchNamer.Compose(new TicketId(tip.RequiredTicketId)).Value
            : await LandedOnAsync(project.Name, edge, repo, provider, ct) ?? connection.DefaultBranch;
        if (!string.IsNullOrWhiteSpace(to) && to != current) return new PullRequestMove(repo, url, current, to);
        logger.LogWarning("{Url} sits on the landed branch '{Current}' but no base to move it to was found (no pull request "
            + "of {Required} in {Repo}, no default_branch configured)", url, current, edge.RequiredTicketId, repo);
        return null;
    }

    // The base the prerequisite's own pull request in this repo had — where its work landed.
    private async Task<string?> LandedOnAsync(
        string project, TicketRequirementEdge edge, string repo, ISourceProvider provider, CancellationToken ct) =>
        (await attempts.LatestAsync(project, edge.RequiredTicketId, null, ct))?.PullRequestUrls.TryGetValue(repo, out var url) == true
            ? await provider.ReadPullRequestBaseAsync(url, ct)
            : null;
}
