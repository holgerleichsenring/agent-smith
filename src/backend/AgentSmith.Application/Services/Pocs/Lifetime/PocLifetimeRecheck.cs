using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Lifetime;

/// <summary>
/// 2026-11-23-c81ee: the PocAct step's re-check of a lifetime case — the PoC re-read now, put through the same
/// <see cref="PocLifetimePlan"/> the pass decided with. Still due: the audit act and reason the run records. No longer due
/// (extended meanwhile, a new upload, deleted already): why there is nothing to do, and the run ends green saying so.
/// </summary>
public sealed class PocLifetimeRecheck(
    IPocLifetimeStore store, PocLifetime lifetime, PocLifetimePlan plan, PocLifetimeReasons reasons, TimeProvider clock)
{
    public async Task<(PocAuditAct? Audit, string? Settled)> CheckAsync(string lifetimeCase, long pocId, PocsConfig pocs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pocs);
        if (await store.ReadAsync(pocId, ct) is not { } poc) return (null, $"PoC {pocId} is gone");
        if (poc.State == PocStates.Deleted) return (null, $"'{poc.Name}' is deleted already");
        var now = clock.GetUtcNow();
        var state = lifetime.TargetOf(pocs, poc.Target) is { } target ? lifetime.Of(poc.Lifetime, target, now) : null;
        var due = plan.CaseOf(poc, state, pocs.Lifetime, now);
        if (due == lifetimeCase) return (reasons.Of(lifetimeCase, pocs), null);
        return (null, lifetimeCase == PocLifetimeCases.Removal
            ? $"'{poc.Name}' is no longer a blocked PoC without its upload (it is {poc.State}, revision {poc.Revision?.ToString() ?? "none"})"
            : state is { IsExpired: false }
                ? $"'{poc.Name}' is no longer expired: it expires on {state.ExpiresAt:yyyy-MM-dd}"
                : $"'{poc.Name}' is no longer due for {lifetimeCase} (due now: {due ?? "nothing"})");
    }
}
