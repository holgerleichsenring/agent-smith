using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.Init;

/// <summary>
/// p0489: the SYNCHRONOUS admission gate for a manual init. Same sequence the
/// spawn path admits a ticket with — footprint, record, namespace quota probe,
/// atomic reserve — but a run that does not fit is REFUSED with the reason instead
/// of queued: the capacity queue re-validates a ticket's native status every tick
/// and a ticketless entry has nothing to re-validate. A refused launch releases the
/// recorded footprint, so it leaves no reservation behind.
/// <para>
/// 2026-08-27-7098: the corpse sweep is NOT here. It is leader housekeeping —
/// two namespace-wide pod listings, a live-run read across the database and Redis,
/// and one untimed delete per corpse — and running it before answering made the
/// operator wait through all of it for a verdict none of it decides. It still runs
/// on the housekeeping loop and in the capacity queue's own tick, which is where a
/// sweep that frees quota for a QUEUED run belongs.
/// </para>
/// <para>
/// 2026-09-21-5c17: the two sentences a refusal can carry are no longer written here. The
/// spawn funnel's reservation now answers with this same decision type, and one copy of the
/// ledger's sentence — and of the fallback that keeps a wordless denial off a run row —
/// serves both doors. The corpse sweep's absence above is NOT part of what is shared.
/// </para>
/// </summary>
public sealed class InitRunAdmission(
    IRunFootprintCalculator footprintCalculator,
    ICapacityBudget capacityBudget,
    IHeldSandboxRegister heldSandboxes,
    ISandboxCapacityProbe capacityProbe,
    ILogger<InitRunAdmission> logger)
{
    public async Task<CapacityDecision> TryAdmitAsync(
        ResolvedProject project, string pipelineName, string runId, CancellationToken ct)
    {
        var footprint = await footprintCalculator.CalculateAsync(project, pipelineName, ct);
        await capacityBudget.RecordAsync(runId, footprint, ct);
        // 2026-09-22-2d11a: releasing held sandboxes is a force remove with no grace, which
        // is why it belongs at the door the corpse sweep was taken out of. It is bounded by
        // what this process holds — nothing, until a design conversation holds one.
        await heldSandboxes.EvictAsync(ct);

        var quota = await capacityProbe.HasCapacityAsync(RunFootprint.From(footprint), ct);
        if (!quota.Admitted)
            return await RefuseAsync(runId, CapacityReasons.Carried(quota.Reason), ct);

        if (!await capacityBudget.TryReserveAsync(runId, ct))
            return await RefuseAsync(runId, CapacityReasons.LedgerFull(footprint), ct);

        return CapacityDecision.Admit();
    }

    // Releasing DELETES the recorded footprint row, so a refused launch is
    // indistinguishable from one that never happened.
    private async Task<CapacityDecision> RefuseAsync(string runId, string reason, CancellationToken ct)
    {
        await capacityBudget.ReleaseAsync(runId, ct);
        logger.LogInformation("Init admission refused for run {RunId}: {Reason}", runId, reason);
        return CapacityDecision.Deny(reason);
    }
}
