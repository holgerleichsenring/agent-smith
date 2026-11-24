using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>
/// 2026-11-23-c81ec: writes how a follow ended and says it. Settled: the PoC ready, or stopped for desired 0 — the one
/// state table every act and poc-deploy follow (2026-11-23-c81ed) — and the revision's rollout state healthy. Failed or
/// late: the rollout state failed, and the PoC keeps its state when a revision rolled out healthy before (the old pods
/// still serve: maxUnavailable 0), else 933da's failed.
/// <para>2026-11-23-c81ed: an act writes the rollout state only when it changed the digest (a rollback, on the revision it
/// rolled to); a stop, start or apply leaves it untouched, and any failed act leaves the PoC's state exactly as it was —
/// a transient Argo issue must not mark a healthy revision failed. A settled delete (the Application answers 404) deletes
/// the PoC and frees its name; a failed delete writes nothing.</para>
/// </summary>
public sealed class PocRolloutRecord(IPocRolloutStore store, TimeProvider clock)
{
    public async Task<CommandResult> EndAsync(PocRolloutEnd end, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(end);
        if (end.Expected.Absent) return await GoneAsync(end, ct);
        return end.Reading.Kind == PocRolloutReadingKind.Settled ? await SettledAsync(end, ct) : await FailedAsync(end, ct);
    }

    private async Task<CommandResult> FailedAsync(PocRolloutEnd end, CancellationToken ct)
    {
        var why = end.Reading.Kind == PocRolloutReadingKind.Failed
            ? $"failed to roll out: Argo CD reports {end.Reading.Message} ({end.Reading.Line})"
            : $"did not settle within {end.Budget.Bound}; last reading: {end.Reading.Line}";
        if (end.Act is { } act && act != PocActKinds.Rollback)
            return CommandResult.Fail($"The {act} of '{end.Name}' {why}; its state and its revisions' rollout states are left as they were.");
        if (end.Act is not null)
        {
            await store.RecordOutcomeAsync(new PocRolloutOutcome(end.PocId, end.Revision, PocRolloutStates.Failed, clock.GetUtcNow(), null), ct);
            return CommandResult.Fail($"The rollback of '{end.Name}' to revision {end.Revision} {why}; revision {end.Revision} is marked failed and the PoC's state is left as it was.");
        }
        var kept = await store.HasHealthyRolloutAsync(end.PocId, ct);
        await store.RecordOutcomeAsync(new PocRolloutOutcome(end.PocId, end.Revision, PocRolloutStates.Failed, clock.GetUtcNow(),
            kept ? null : PocStates.Failed), ct);
        var serving = kept ? "the previous version keeps serving (maxUnavailable 0)" : $"'{end.Name}' never ran, so it is failed";
        return CommandResult.Fail($"Revision {end.Revision} of '{end.Name}' {why}; {serving}.");
    }

    private async Task<CommandResult> GoneAsync(PocRolloutEnd end, CancellationToken ct)
    {
        if (end.Reading.Kind != PocRolloutReadingKind.Settled)
            return CommandResult.Fail($"The Application {end.Expected.Application} still exists after {end.Budget.Bound}: {end.Reading.Line}. "
                + "The deploy folder is gone, so Argo CD prunes it once it syncs; the delete stays pending until a retry sees it gone.");
        await store.RecordDeletedAsync(end.PocId, ct);
        return CommandResult.Ok($"The Application {end.Expected.Application} is gone; Argo CD pruned '{end.Name}'. The PoC is deleted and its "
            + "name and host are free.");
    }

    private async Task<CommandResult> SettledAsync(PocRolloutEnd end, CancellationToken ct)
    {
        var state = end.Expected.Replicas > 0 ? PocStates.Ready : PocStates.Stopped;
        var rollout = end.Act is null || end.Act == PocActKinds.Rollback ? PocRolloutStates.Healthy : null;
        await store.RecordOutcomeAsync(new PocRolloutOutcome(end.PocId, end.Revision, rollout, clock.GetUtcNow(), state), ct);
        var where = end.Expected.Replicas > 0 && end.Host is { } host ? $" '{end.Name}' answers at https://{host}." : string.Empty;
        return CommandResult.Ok($"Revision {end.Revision} of '{end.Name}' is Synced and Healthy with {end.Expected.Replicas} "
            + $"replica(s) of this render; the PoC is {state}.{where}");
    }
}
