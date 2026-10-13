using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Events;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the live joins the bucket rule needs, read once per request — the capacity
/// queue's positions, the relaunch leases of the parked heads (one lease read per waiting head)
/// and every pending checkpoint. Computed per request, never cached, so replicas cannot disagree.
/// </summary>
public sealed class TicketLiveStateReader(
    ICapacityQueue capacityQueue,
    IActiveRunLease activeRunLease,
    IRunCheckpointStore checkpoints,
    TimeProvider clock,
    ILogger<TicketLiveStateReader> logger)
{
    public async Task<TicketLiveState> ReadAsync(IReadOnlyList<Run> heads, CancellationToken ct)
    {
        var positions = await capacityQueue.GetPositionsByRunIdAsync(ct);
        var relaunching = await RunQueuePlace.RelaunchingAsync(activeRunLease, clock, positions, heads, ct);
        var pending = new Dictionary<string, PendingQuestionInfo>(StringComparer.Ordinal);
        foreach (var checkpoint in await checkpoints.ListPendingAsync(ct))
            if (PendingQuestionInfo.FromCheckpoint(checkpoint, logger) is { } question)
                pending[checkpoint.RunId] = question;
        return new TicketLiveState(positions, relaunching, pending);
    }
}
