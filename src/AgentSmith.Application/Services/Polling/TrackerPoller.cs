using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
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
    ILogger<TrackerPoller> logger) : IEventPoller
{
    public string PlatformName => tracker.Type.ToString();
    public string TrackerName => tracker.Name;
    public int IntervalSeconds => tracker.Polling.IntervalSeconds;

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
        var pending = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, ct);
        var discovered = await provider.ListOpenAsync(ct);
        return LifecyclePollFilter.KeepClaimable(MergeDistinct(pending, discovered)).ToList();
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
        var matches = envelopeResolver.Resolve(config, envelope);
        if (matches.Count == 0)
        {
            counts.ZeroMatched++;
            logger.LogInformation(
                "polling-zero-match: tracker={Tracker} ticket={Ticket} labels=[{Labels}]",
                tracker.Name, ticket.Id.Value, string.Join(",", ticket.Labels));
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
            logger.LogInformation(
                "polling-status-filter: ticket={Ticket} status='{Status}' not in trigger_statuses for project '{Project}'",
                ticket.Id.Value, ticket.Status, project.Name);
            return;
        }

        await spawnUseCase.ExecuteAsync(config, project, match.PipelineName, envelope, trigger, ct);
        counts.Spawned++;
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
