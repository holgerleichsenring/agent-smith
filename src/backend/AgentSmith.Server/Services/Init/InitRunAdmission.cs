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
/// </summary>
public sealed class InitRunAdmission(
    IRunFootprintCalculator footprintCalculator,
    ICapacityBudget capacityBudget,
    ISandboxCapacityProbe capacityProbe,
    ILogger<InitRunAdmission> logger)
{
    public async Task<CapacityDecision> TryAdmitAsync(
        ResolvedProject project, string pipelineName, string runId, CancellationToken ct)
    {
        var footprint = await footprintCalculator.CalculateAsync(project, pipelineName, ct);
        await capacityBudget.RecordAsync(runId, footprint, ct);

        var quota = await capacityProbe.HasCapacityAsync(RunFootprint.From(footprint), ct);
        if (!quota.Admitted)
            return await RefuseAsync(runId, quota.Reason ?? "the namespace quota is full", ct);

        if (!await capacityBudget.TryReserveAsync(runId, ct))
            return await RefuseAsync(runId, BudgetReason(footprint), ct);

        return CapacityDecision.Admit();
    }

    private static string BudgetReason(RunFootprintBreakdown footprint) =>
        $"no capacity — footprint {footprint.TotalMemLimit} / {footprint.TotalCpuLimit} cpu "
        + "exceeds the remaining budget";

    // Releasing DELETES the recorded footprint row, so a refused launch is
    // indistinguishable from one that never happened.
    private async Task<CapacityDecision> RefuseAsync(string runId, string reason, CancellationToken ct)
    {
        await capacityBudget.ReleaseAsync(runId, ct);
        logger.LogInformation("Init admission refused for run {RunId}: {Reason}", runId, reason);
        return CapacityDecision.Deny(reason);
    }
}
