using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>
/// 2026-11-23-c81ec: writes how a follow ended and says it. Settled: the revision's rollout state healthy, and the PoC
/// ready, or stopped for desired 0. Failed or late: the rollout state failed, and the PoC keeps its state when a revision
/// rolled out healthy before (the old pods still serve: maxUnavailable 0), else 933da's failed. An expected absence
/// (poc-delete) writes nothing here — the acts' outcomes are 2026-11-23-c81ed's.
/// </summary>
public sealed class PocRolloutRecord(IPocRolloutStore store, TimeProvider clock)
{
    public async Task<CommandResult> EndAsync(PocRolloutEnd end, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(end);
        if (end.Expected.Absent)
            return end.Reading.Kind == PocRolloutReadingKind.Settled
                ? CommandResult.Ok($"The Application {end.Expected.Application} is gone; Argo CD pruned '{end.Name}'.")
                : CommandResult.Fail($"The Application {end.Expected.Application} still exists after {end.Budget.Bound}: {end.Reading.Line}.");
        if (end.Reading.Kind == PocRolloutReadingKind.Settled) return await SettledAsync(end, ct);
        var kept = await store.HasHealthyRolloutAsync(end.PocId, ct);
        await store.RecordOutcomeAsync(new PocRolloutOutcome(end.PocId, end.Revision, PocRolloutStates.Failed, clock.GetUtcNow(),
            kept ? null : PocStates.Failed), ct);
        var why = end.Reading.Kind == PocRolloutReadingKind.Failed
            ? $"failed to roll out: Argo CD reports {end.Reading.Message} ({end.Reading.Line})"
            : $"did not settle within {end.Budget.Bound}; last reading: {end.Reading.Line}";
        var serving = kept ? "the previous version keeps serving (maxUnavailable 0)" : $"'{end.Name}' never ran, so it is failed";
        return CommandResult.Fail($"Revision {end.Revision} of '{end.Name}' {why}; {serving}.");
    }

    private async Task<CommandResult> SettledAsync(PocRolloutEnd end, CancellationToken ct)
    {
        var state = end.Expected.Replicas > 0 ? PocStates.Ready : PocStates.Stopped;
        await store.RecordOutcomeAsync(new PocRolloutOutcome(end.PocId, end.Revision, PocRolloutStates.Healthy, clock.GetUtcNow(), state), ct);
        var where = end.Expected.Replicas > 0 && end.Host is { } host ? $" '{end.Name}' answers at https://{host}." : string.Empty;
        return CommandResult.Ok($"Revision {end.Revision} of '{end.Name}' is Synced and Healthy with {end.Expected.Replicas} "
            + $"replica(s) of this render; the PoC is {state}.{where}");
    }
}
