using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Spawning;

/// <summary>
/// 2026-09-13-a72a: holds a ticket back while a ticket it follows is still in the working set,
/// so the order the cut decided is the order the runs happen in.
/// <para>
/// 2026-09-17-0e79d: nothing the framework files carries a predecessor stamp any more. An
/// approved epic is ONE work ticket, and the order inside it is the sequence's — phase by phase,
/// a successor held until its predecessor VERIFIED, which is stronger than any status gate. What
/// reaches this gate now is a legacy epic child already on a tracker or a hand-stamped ticket,
/// and both are still ordered exactly as they were.
/// </para>
/// <para>
/// The done-signal is "OUTSIDE the project's trigger_statuses", NOT "equals DoneStatus":
/// DoneStatus defaults to "In Review", which means a pull request was opened, and an
/// operator who closes a ticket by hand to some other terminal status would otherwise block
/// its successor forever. Leaving the working set is the predicate TrackerPoller and
/// CapacityQueuePump already use to decide a ticket is no longer theirs.
/// </para>
/// <para>
/// The walk follows a resolved predecessor's own predecessors, so the whole chain is
/// honoured, and every tracker read is a HOP. Beyond <see cref="MaxHops"/> the chain is
/// reported unresolvable and the ticket is NOT waited on: a cycle can only enter through a
/// hand-edited label — RequiresEdgeChecker refuses a cyclic epic before the outcome is
/// proposed — and a blocked-forever ticket is a worse failure than an out-of-order run.
/// A tracker read that throws is not caught: the funnel has reserved nothing at this point,
/// so the poll retries next tick rather than dropping the ticket on "tracker unreachable".
/// </para>
/// </summary>
public sealed class PredecessorGate(
    ITicketProviderFactory ticketFactory,
    ILogger<PredecessorGate> logger) : IPredecessorGate
{
    private const int MaxHops = 16;

    public async Task<PredecessorVerdict> CheckAsync(
        ResolvedProject project, IncomingTicketEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(envelope);
        var pending = new Queue<string>(FiledTicketLabels.PredecessorIds(envelope.Labels));
        if (pending.Count == 0) return PredecessorVerdict.Ready();

        var trigger = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type);
        if (trigger is null || trigger.TriggerStatuses.Count == 0)
        {
            // With no trigger_statuses the project cannot express "left the working set" —
            // every status triggers, so every predecessor would block forever.
            logger.LogWarning(
                "Predecessor gate cannot order ticket {Ticket}: project {Project} declares no "
                + "trigger_statuses, so no status means a slice is done",
                envelope.TicketId, project.Name);
            return PredecessorVerdict.Ready();
        }

        var provider = ticketFactory.Create(project.Tracker);
        for (var hop = 0; pending.Count > 0; hop++)
        {
            if (hop >= MaxHops)
            {
                logger.LogWarning(
                    "Predecessor chain of ticket {Ticket} in project {Project} exceeds {MaxHops} "
                    + "hops — reported unresolvable, the ticket is not waited on",
                    envelope.TicketId, project.Name, MaxHops);
                return PredecessorVerdict.Ready();
            }
            var id = pending.Dequeue();
            var ticket = await provider.GetTicketAsync(new TicketId(id), cancellationToken);
            if (trigger.TriggerStatuses.Contains(ticket.Status, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Spawn held for project={Project} ticket={Ticket}: slice #{Predecessor} is "
                    + "still '{Status}'", project.Name, envelope.TicketId, id, ticket.Status);
                return PredecessorVerdict.Wait($"waiting for slice #{id}, still '{ticket.Status}'");
            }
            foreach (var next in FiledTicketLabels.PredecessorIds(ticket.Labels))
                pending.Enqueue(next);
        }
        return PredecessorVerdict.Ready();
    }
}
