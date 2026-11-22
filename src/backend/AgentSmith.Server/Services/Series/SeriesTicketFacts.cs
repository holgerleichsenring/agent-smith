using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-17-0207a: what is true of a series' member tickets, read where it is kept — the landings, the requires
/// edges, every run of the ticket (cost, first start), the newest attempt and a question it is parked on.
/// </summary>
public sealed class SeriesTicketFacts(
    TicketProjects projects, ITicketLandingStore landings, ITicketRequirementStore requirements,
    TicketRunsQuery runs, IPreviousAttemptReader attempts, SeriesParkedQuestion parked, SeriesTicketCi ci,
    Preconditions.InterruptedRestartWindow? restart = null, // 2026-11-22-6d85c
    Application.Services.Claim.UnmetTicketRequirements? unmet = null) : ISeriesTicketFacts // 2026-11-22-6d85d
{
    public async Task<IReadOnlyList<SeriesTicketFact>> ReadAsync(
        string project, IReadOnlyList<SeriesTicket> tickets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        if (projects.Of(project) is not { } resolved) return [.. tickets.Select(t => new SeriesTicketFact(t))];
        var landed = (await landings.LandedAsync(resolved.Tracker.Name, [.. tickets.Select(t => Key(resolved, t))], cancellationToken))
            .ToDictionary(l => l.TicketId, l => l.LandedAt, StringComparer.Ordinal);
        var merges = await MergesAsync(resolved, tickets, cancellationToken);
        var facts = new List<SeriesTicketFact>();
        foreach (var ticket in tickets)
            facts.Add(await ReadOneAsync(resolved, ticket, LandedAt(landed, ticket.TicketId), cancellationToken)
                with { StartsOnceMerged = merges.GetValueOrDefault(Key(resolved, ticket)) ?? [] });
        return facts;
    }

    // 2026-11-22-6d85d: the merges that unblock each held ticket, read set-based by the rule a start reads.
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> MergesAsync(
        ResolvedProject project, IReadOnlyList<SeriesTicket> tickets, CancellationToken ct) =>
        unmet is null || project.RequiresStart != RequiresStart.OnGreenPush ? new Dictionary<string, IReadOnlyList<string>>()
            : (await unmet.ForStartManyAsync(project, [.. tickets.Select(t => Key(project, t)).Distinct()], ct))
                .ToDictionary(p => p.Key, p => (IReadOnlyList<string>)[.. p.Value.MergesToStart.Select(e => e.RequiredTicketId)]);

    private async Task<SeriesTicketFact> ReadOneAsync(
        ResolvedProject project, SeriesTicket ticket, DateTimeOffset? landedAt, CancellationToken ct)
    {
        var edges = await requirements.RequiredOfAsync(project.Tracker.Name, Key(project, ticket), ct);
        var rows = await runs.ForTicketAsync(project.Name, ticket.TicketId, ct);
        var latest = await attempts.LatestAsync(project.Name, ticket.TicketId, null, ct);
        var fact = new SeriesTicketFact(ticket)
        {
            Requires = [.. edges.Select(e => e.RequiredTicketId)], LandedAt = landedAt,
            FirstStartedAt = rows.Count == 0 ? null : rows.Min(r => r.Run.StartedAt),
            CostUsd = rows.Sum(r => r.Run.CostTotalUsd),
        };
        return latest is null ? fact : await ci.WithAsync(await parked.WithAsync(Attempt(fact, latest), latest, ct), project.Name, ct);
    }

    private SeriesTicketFact Attempt(SeriesTicketFact fact, PreviousAttempt latest) => fact with
    {
        LatestRunId = latest.RunId, LatestStatus = latest.Status, LatestFinished = latest.Finished,
        OpenedPullRequest = latest.OpenedPullRequest, PullRequests = [.. latest.PullRequestUrls.Values],
        EndDetail = latest.Status == RunStatuses.Success ? null : latest.EndDetail?.Subject ?? latest.CancelReason ?? latest.EndKind,
        // 2026-11-22-6d85c: a lost process is no red; the interrupted restart retries it once
        Interrupted = latest is { Finished: true, Status: LeftInPlaceEnd.Cancelled } && OwedWorkCancel.IsOwed(latest.CancelReason),
        // the restart retries it once, within its window; otherwise the row is red for a person
        Restarting = !(latest.CauseChannel == AttemptChannels.Retry && latest.CauseActor == AttemptChannels.RestartActor)
            && restart?.Covers(latest.FinishedAt) == true,
    };

    // 2026-11-18: a ticket with no landing has no landing time; GetValueOrDefault on a non-nullable value handed back
    // 0001-01-01, so every ticket read as landed.
    internal static DateTimeOffset? LandedAt(IReadOnlyDictionary<string, DateTimeOffset> landed, string ticketId) =>
        landed.TryGetValue(ticketId, out var at) ? at : null;

    private static string Key(ResolvedProject project, SeriesTicket ticket) =>
        TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticket.TicketId).Value;
}
