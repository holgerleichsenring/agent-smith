using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// p0140c: per-tracker poller. Pulls all open tickets from one tracker connection, routes
/// each through IEnvelopeProjectResolver, and spawns pipelines per matched project. Replaces
/// the four per-platform per-project pollers — post-p0140c the loop is identical across
/// platforms (the platform string comes from TrackerConnection.Type), so collapsing them
/// into one class removes 4 files of duplicated logic.
/// </summary>
public sealed class TrackerPoller(
    TrackerConnection tracker,
    AgentSmithConfig config,
    ITicketProviderFactory ticketFactory,
    IEnvelopeProjectResolver envelopeResolver,
    ISpawnPipelineRunsUseCase spawnUseCase,
    IActiveRunLease activeRunLease,
    ISystemEventPublisher systemEvents,
    ITrackerDiscoveryQueryBuilder discoveryQueryBuilder,
    ILogger<TrackerPoller> logger) : IEventPoller
{
    public string PlatformName => tracker.Type.ToString();
    public string TrackerName => tracker.Name;
    public int IntervalSeconds => tracker.Polling.IntervalSeconds;
    private string Source => $"tracker:{tracker.Type.ToString().ToLowerInvariant()}/{tracker.Name}";

    public async Task<PollResult> PollAsync(CancellationToken ct)
    {
        var provider = ticketFactory.Create(tracker);
        var tickets = await PullOpenTicketsAsync(provider, ct);
        if (tickets.Count == 0) return PollResult.Empty();

        var counts = new TrackerPollCounts(tickets.Count);
        foreach (var ticket in tickets)
            await DispatchTicketAsync(ticket, counts, ct);

        logger.LogInformation(
            "Polled tracker '{Tracker}' ({Type}): {Polled} tickets, {Matched} matched, "
            + "{Spawned} spawned, {StatusFiltered} status-filtered, {ZeroMatched} zero-matched",
            tracker.Name, tracker.Type, counts.Polled, counts.Matched,
            counts.Spawned, counts.StatusFiltered, counts.ZeroMatched);

        return counts.ToResult();
    }

    private async Task<IReadOnlyList<Ticket>> PullOpenTicketsAsync(
        ITicketProvider provider, CancellationToken ct)
    {
        // Both reads are LIVE (no cache): the Pending-tag catch-up query and the
        // open-state discovery query hit the tracker API every cycle.
        var pending = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, ct);
        // p0283b: compose the discovery query from each routed project's per-tracker trigger
        // (status + resolution criterion) so the tracker returns only claimable candidates.
        // Providers that can't push it (GitHub/GitLab) fall back to the broad open query.
        var query = discoveryQueryBuilder.Build(config, tracker);
        var discovered = await provider.ListClaimableAsync(query, ct);
        logger.LogDebug("poll-discovery: tracker={Tracker} branches={Branches} parking=[{Parking}]",
            tracker.Name, query.Branches.Count, string.Join(",", query.ParkingStatuses));
        // p0262: lifecycle tags no longer gate claimability (the LifecyclePollFilter is
        // gone). Every discovered/pending-tagged ticket is a candidate; the real gates run
        // per-ticket downstream — the native-status check (IsStatusAllowed against
        // trigger_statuses) and the lease skip (in-flight). Tags are pure markers.
        var claimable = MergeDistinct(pending, discovered);

        // p0238 diagnostics: log each candidate's ACTUAL lifecycle labels + which query
        // surfaced it, so a re-trigger loop is explained by what state the ticket is really
        // in at decision time, not guessed.
        var pendingIds = pending.Select(p => p.Id.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var t in claimable)
        {
            var lifecycle = string.Join(",", (t.Labels ?? Array.Empty<string>())
                .Where(l => l.StartsWith(LifecycleLabels.Prefix, StringComparison.Ordinal)));
            logger.LogInformation(
                "poll-claimable: tracker={Tracker} ticket={Ticket} state={State} lifecycle=[{Lifecycle}] source={Source}",
                tracker.Name, t.Id.Value, t.Status,
                string.IsNullOrEmpty(lifecycle) ? "<none>" : lifecycle,
                pendingIds.Contains(t.Id.Value) ? "pending-tag-query" : "open-discovery-query");
        }
        return claimable;
    }

    private static IReadOnlyList<Ticket> MergeDistinct(
        IReadOnlyList<Ticket> a, IReadOnlyList<Ticket> b)
    {
        var merged = new Dictionary<string, Ticket>();
        foreach (var t in a) merged[t.Id.Value] = t;
        foreach (var t in b) merged.TryAdd(t.Id.Value, t);
        return [.. merged.Values];
    }

    private async Task DispatchTicketAsync(Ticket ticket, TrackerPollCounts counts, CancellationToken ct)
    {
        var envelope = BuildEnvelope(ticket);
        await TryPublishSystemAsync(new TicketScannedEvent(
            Source, tracker.Name, ticket.Id.Value,
            (IReadOnlyList<string>)(ticket.Labels?.ToArray() ?? Array.Empty<string>()),
            DateTimeOffset.UtcNow), ct);

        var matches = envelopeResolver.Resolve(config, envelope);
        if (matches.Count == 0)
        {
            counts.ZeroMatched++;
            logger.LogDebug(
                "polling-zero-match: tracker={Tracker} ticket={Ticket} labels=[{Labels}]",
                tracker.Name, ticket.Id.Value, string.Join(",", ticket.Labels ?? Array.Empty<string>()));
            await TryPublishSystemAsync(new TicketSkippedEvent(
                Source, tracker.Name, ticket.Id.Value,
                TicketSkipReason.ZeroMatch,
                $"no project trigger matched labels=[{string.Join(",", ticket.Labels ?? Array.Empty<string>())}]",
                DateTimeOffset.UtcNow), ct);
            return;
        }

        foreach (var match in matches)
            await SpawnIfStatusAllowedAsync(match, ticket, envelope, counts, ct);
    }

    private async Task SpawnIfStatusAllowedAsync(
        ProjectMatch match, Ticket ticket, IncomingTicketEnvelope envelope,
        TrackerPollCounts counts, CancellationToken ct)
    {
        counts.Matched++;
        var project = config.Projects[match.ProjectName];
        var trigger = TriggerSelectionHelper.ByTrackerType(project, tracker.Type);
        if (trigger is null) return;

        if (!IsStatusAllowed(trigger, ticket.Status))
        {
            counts.StatusFiltered++;
            var detail = $"status '{ticket.Status}' not in trigger_statuses [{string.Join(",", trigger.TriggerStatuses)}] for project '{project.Name}'";
            logger.LogInformation("polling-status-filter: ticket={Ticket} {Detail}", ticket.Id.Value, detail);
            await TryPublishSystemAsync(new TicketSkippedEvent(
                Source, tracker.Name, ticket.Id.Value,
                TicketSkipReason.StatusFilter, detail, DateTimeOffset.UtcNow), ct);
            return;
        }

        // p0262: in-flight gating is the LEASE, not a lifecycle tag. The native status
        // stays in trigger_statuses for the whole run (it only moves at run-end), so a
        // running ticket re-appears as a discovery candidate every poll. Skip it while a
        // lease exists — a live run holds it; a dead run's stale lease is released by the
        // ActiveRunReaper, after which the ticket (still natively open) is claimed. This is
        // the pre-filter that avoids a claim-attempt-then-AlreadyClaimed churn each cycle;
        // the lease INSERT in the claim is still the atomic guard.
        if (await activeRunLease.GetByTicketAsync(match.ProjectName, ticket.Id, ct) is not null)
        {
            logger.LogDebug(
                "poll-inflight-skip: tracker={Tracker} ticket={Ticket} project={Project} — lease held",
                tracker.Name, ticket.Id.Value, match.ProjectName);
            return;
        }

        var spawn = await spawnUseCase.ExecuteAsync(config, project, match.PipelineName, envelope, trigger, ct);
        counts.Spawned++;
        var outcome = spawn.ClaimResults.Count > 0
            ? spawn.ClaimResults[0].Outcome.ToString()
            : "Unknown";
        await TryPublishSystemAsync(new TicketTriggeredEvent(
            Source, tracker.Name, ticket.Id.Value,
            project.Name, match.PipelineName, outcome,
            DateTimeOffset.UtcNow), ct);
    }

    // Fire-and-warn: a publish failure must not break the polling loop.
    private async Task TryPublishSystemAsync(SystemEvent ev, CancellationToken ct)
    {
        try { await systemEvents.PublishAsync(ev, ct); }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to publish system event {Type} from {Source}", ev.Type, ev.Source);
        }
    }

    private IncomingTicketEnvelope BuildEnvelope(Ticket ticket) => new()
    {
        Labels = ticket.Labels,
        TicketId = ticket.Id.Value,
        Platform = tracker.Type.ToString().ToLowerInvariant(),
    };

    private static bool IsStatusAllowed(WebhookTriggerConfig trigger, string status) =>
        trigger.TriggerStatuses.Count == 0
        || trigger.TriggerStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Mutable cycle counts accumulated by TrackerPoller; converted to PollResult on return.</summary>
internal sealed class TrackerPollCounts(int polled)
{
    public int Polled { get; } = polled;
    public int Matched;
    public int Spawned;
    public int StatusFiltered;
    public int ZeroMatched;

    public PollResult ToResult() => new(Polled, Matched, Spawned, StatusFiltered, ZeroMatched);
}
