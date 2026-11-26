using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services;

/// <summary>
/// 2026-11-26-a12ec: what the capacity-queue pump asks of its head before it launches it. A head whose ticket waits on a
/// prerequisite that has not landed is held by the claim's own gate (<see cref="RequiresUnmetGate"/>) — read before the
/// reservation, so a dependent queued ahead of its prerequisite is set aside instead of blocking the queue for good. A head
/// the capacity ledger refuses gets the ledger's words as its reason; the probe's words from its enqueue are no longer true.
/// </summary>
public sealed class CapacityQueueHead(ICapacityBudget budget, ICapacityQueue queue, RequiresUnmetGate? requires = null)
{
    /// <summary>The claim's refusal of the head's ticket now, or null. A resume is a started run, never held.</summary>
    public Task<ClaimResult?> HeldAsync(CapacityQueueEntry head, ClaimRequest request, AgentSmithConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(head);
        return requires is null || head.IsResume ? Task.FromResult<ClaimResult?>(null) : requires.RefusalAsync(request, config, ct);
    }

    /// <summary>Reserves the head's footprint; on a refusal its entry and queued run say the ledger's reason.</summary>
    public async Task<bool> ReserveAsync(CapacityQueueEntry head, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(head);
        if (head.ReservedRunId is null) return false;
        if (await budget.TryReserveAsync(head.ReservedRunId, ct)) return true;
        var reason = await budget.GetAsync(head.ReservedRunId, ct) is { } snapshot
            ? CapacityReasons.LedgerFull(snapshot.Footprint) : "the capacity ledger has no room for this run";
        if (reason != head.Reason) await queue.ReasonAsync(head.Project, head.TicketId, reason, ct);
        return false;
    }
}
