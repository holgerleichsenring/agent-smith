using System.Text.Json;
using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0320c: the capacity queue's dequeue point. Every tick it peeks the HEAD entry
/// (strict FIFO — nothing behind it is considered), re-validates the ticket's
/// native status against the project's trigger config (the operator may have
/// closed it → drop the entry and cancel its queued run row), probes the run's
/// footprint, and on admission claims with the reserved run id so the queued Run
/// row becomes the running row. Entries without an envelope (InitialContextJson
/// null — the projector's TOCTOU backstop) are left for the poller funnel, which
/// claims a head ticket itself with a fresh envelope.
/// </summary>
public sealed class CapacityQueuePump(
    ICapacityQueue queue,
    ITicketClaimService claimService,
    ITicketProviderFactory ticketFactory,
    ISandboxResourceResolver resourceResolver,
    IOrchestratorResourceResolver orchestratorResolver,
    ISandboxCapacityProbe capacityProbe,
    IEventPublisher events,
    IRunCancelStateReader cancelState,
    IConfigurationLoader configLoader,
    string configPath,
    ILogger<CapacityQueuePump> logger)
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);

    public async Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("CapacityQueuePump started (tick: {Interval})", TickInterval);
        while (!ct.IsCancellationRequested)
        {
            try { await TickAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Capacity-queue tick failed"); }

            try { await Task.Delay(TickInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    // Internal so the p0320c tests drive single ticks without the 15s loop.
    internal async Task TickAsync(CancellationToken ct)
    {
        var head = await queue.PeekHeadAsync(ct);
        if (head is null) return;
        // p0330: pre-claim cancel gate — a cancel persisted while the entry waited
        // at the head must drop it, never claim it. Checked before the envelope
        // guard so backstop entries are cleaned up too.
        if (!string.IsNullOrEmpty(head.ReservedRunId)
            && await cancelState.IsCancelRequestedAsync(head.ReservedRunId!, ct))
        {
            await DropAsync(head, "cancelled by operator", ct);
            return;
        }
        if (head.InitialContextJson is null) return; // TOCTOU-backstop entry — poller launches it

        var config = configLoader.LoadConfig(configPath);
        if (!config.Projects.TryGetValue(head.Project, out var project))
        {
            await DropAsync(head, $"project '{head.Project}' is no longer configured", ct);
            return;
        }
        if (!await IsStillTriggeredAsync(project, head, ct))
        {
            await DropAsync(head, "ticket left its trigger statuses", ct);
            return;
        }

        var capacity = await capacityProbe.HasCapacityAsync(FootprintOf(project, head.Pipeline), ct);
        if (!capacity.Admitted) return; // still no room — the head keeps its place

        var result = await claimService.ClaimAsync(ToClaimRequest(head), config, ct);
        if (result.Outcome == ClaimOutcome.Claimed)
            await queue.RemoveAsync(head.Project, head.TicketId, ct);
        logger.LogInformation(
            "Capacity-queue head {Project}/#{Ticket} (run {RunId}) launch → {Outcome}",
            head.Project, head.TicketId, head.ReservedRunId, result.Outcome);
    }

    // A tracker read failure throws and is retried next tick (never drop an entry
    // on "tracker unreachable" — only on a status the operator actually moved).
    private async Task<bool> IsStillTriggeredAsync(
        ResolvedProject project, CapacityQueueEntry head, CancellationToken ct)
    {
        var trigger = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type);
        if (trigger is null) return false;
        var ticket = await ticketFactory.Create(project.Tracker)
            .GetTicketAsync(new TicketId(head.TicketId), ct);
        return trigger.TriggerStatuses.Count == 0
            || trigger.TriggerStatuses.Contains(ticket.Status, StringComparer.OrdinalIgnoreCase);
    }

    // Stale entry: remove it and finish its queued Run row as cancelled — nothing
    // is executing, so the terminal event is the whole teardown (it also nudges
    // the dashboard).
    private async Task DropAsync(CapacityQueueEntry head, string reason, CancellationToken ct)
    {
        await queue.RemoveAsync(head.Project, head.TicketId, ct);
        if (!string.IsNullOrEmpty(head.ReservedRunId))
            await events.PublishAsync(new RunFinishedEvent(
                head.ReservedRunId!, "cancelled", null,
                $"dropped from capacity queue: {reason}", DateTimeOffset.UtcNow), ct);
        logger.LogInformation(
            "Capacity-queue entry {Project}/#{Ticket} dropped: {Reason}",
            head.Project, head.TicketId, reason);
    }

    private RunFootprint FootprintOf(ResolvedProject project, string pipeline) => new(
        orchestratorResolver.Resolve(project),
        Enumerable.Repeat(resourceResolver.Resolve(project, pipeline), project.Repos.Count).ToList());

    private static ClaimRequest ToClaimRequest(CapacityQueueEntry head) => new(
        head.Platform, head.Project, new TicketId(head.TicketId), head.Pipeline,
        InitialContext: Deserialize<Dictionary<string, object>>(head.InitialContextJson),
        PlanAnswers: Deserialize<Dictionary<string, string>>(head.PlanAnswersJson),
        ExistingRunId: head.ReservedRunId);

    // Same JSON round-trip as the Redis job queue, so context values reach the
    // pipeline with identical semantics to a normally enqueued request.
    private static T? Deserialize<T>(string? json) where T : class =>
        json is null ? null : JsonSerializer.Deserialize<T>(json);
}
