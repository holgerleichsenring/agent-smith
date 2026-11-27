using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0320c: the capacity queue's dequeue point. Every tick it peeks the HEAD entry (strict
/// FIFO), re-validates the ticket's native status against the project's trigger config (the
/// operator may have closed it → drop the entry and cancel its queued run row), and on
/// admission claims with the reserved run id so the queued Run row becomes the running row.
/// p0336: admission is the capacity-budget reservation of the head's pre-computed footprint;
/// it launches only when the full footprint fits, else it keeps its place. Entries without
/// an envelope (InitialContextJson null — the projector's TOCTOU backstop) are left for the
/// poller funnel, which claims a head ticket with a fresh envelope.
/// </summary>
public sealed class CapacityQueuePump(
    ICapacityQueue queue,
    ITicketClaimService claimService,
    ITicketProviderFactory ticketFactory,
    ICapacityBudget capacityBudget,
    ISandboxCorpseReaper corpseReaper,
    // 2026-09-21-c724c: a dropped entry's run is FINISHED where its row lives, not merely
    // announced into a stream nothing drains. Order and collaborators live in that class.
    CapacityQueueDrop drop,
    IRunCancelStateReader cancelState,
    ResumeRunLauncher resumeLauncher,
    IConfigurationLoader configLoader,
    string configPath,
    ILogger<CapacityQueuePump> logger, CapacityQueueHead? heads = null, // 2026-11-26-a12ec: a held head is set aside
    AgentSmith.Server.Contracts.IQueueHeadClaim? claims = null) // 2026-11-27-3532a: the head is claimed with the approved set read at its claim
{
    private readonly CapacityQueueHead _heads = heads ?? new(capacityBudget, queue);
    private readonly AgentSmith.Server.Contracts.IQueueHeadClaim _claims = claims ?? new QueueHeadClaim();
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
        // p0330: pre-claim gate — a cancel persisted while the entry waited at the head drops
        // it, never claims it; checked before the envelope guard so backstop entries go too.
        // 2026-10-02-5f89d: a finished reserved row is refused too — not always the operator's.
        if (!string.IsNullOrEmpty(head.ReservedRunId)
            && await cancelState.IsStartRefusedAsync(head.ReservedRunId!, ct))
        {
            await drop.DropAsync(head, "reserved run already cancelled or finished", ct);
            return;
        }
        var config = configLoader.LoadConfig(configPath);
        // 2026-11-26-a12ec: read before the backstop return — no poll relaunches a held ticket, so its entry blocked the queue.
        if (await _heads.HeldAsync(head, QueueHeadClaim.Stored(head), config, ct) is { } held) { await drop.RefusedAsync(head, held, ct); return; }
        if (head.InitialContextJson is null) return; // TOCTOU-backstop entry — poller launches it
        if (!config.Projects.TryGetValue(head.Project, out var project))
        {
            await drop.DropAsync(head, $"project '{head.Project}' is no longer configured", ct);
            return;
        }
        // p0327: a resume skips the trigger-status re-validation (the ticket sits in its WORKING
        // status mid-run), and so does a run a person named in chat, which no status routed.
        if (!head.IsResume && !QueueHeadClaim.IsRequestedByName(head) && !await IsStillTriggeredAsync(project, head, ct))
        {
            await drop.DropAsync(head, "ticket left its trigger statuses", ct);
            return;
        }

        // p0355: reap corpse sandbox pods (a crashed replica's pod still holds the
        // namespace ResourceQuota) before the queued head retries its reservation, so
        // headroom reflects reality on the dequeue path too.
        await corpseReaper.ReapCorpsesAsync(ct);

        // p0336: reserve the head's full pre-computed footprint against the budget.
        // Fits → the reservation is held for the run's whole life (no mid-run fail);
        // does not fit → the head keeps its place and retries next tick.
        if (!await _heads.ReserveAsync(head, ct)) return;

        if (head.IsResume)
        {
            await resumeLauncher.LaunchAsync(head, ct);
            return;
        }

        var result = await claimService.ClaimAsync(await _claims.AtClaimAsync(head, project, ct), config, ct);
        if (result.Outcome == ClaimOutcome.Claimed) await queue.RemoveAsync(head.Project, head.TicketId, ct);
        else if (head.ReservedRunId is not null) await capacityBudget.ReleaseAsync(head.ReservedRunId, ct);
        // c1a7: a rejection is permanent, and this pump looks only at the head — it must not stay (1b72a: a pause's is marked).
        if (result.Outcome == ClaimOutcome.Rejected) await drop.RefusedAsync(head, result, ct);
        logger.LogInformation("Capacity-queue head {Project}/#{Ticket} (run {RunId}) launch → {Outcome}",
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
}
