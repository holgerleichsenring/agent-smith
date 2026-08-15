using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0413: what the scope classifier decided ABOUT the ticket lands on the run row —
/// its size (the resolved budget, p0357) and its shape (which decided how it was
/// cut). Split out of <see cref="RunEventApplier"/> like
/// <see cref="RunSandboxProjection"/>: the applier routes events, this owns what
/// the classification means on the row.
/// </summary>
public sealed class RunClassificationProjection
{
    /// <summary>p0357: the resolved cost budget (tier + cap) from ScopeRepos — the
    /// event stream is the spawned orchestrator's only DB channel.</summary>
    public Task ApplyBudgetAsync(IUnitOfWork uow, RunBudgetResolvedEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        return UpdateAsync(uow, e.RunId, r =>
        {
            r.BudgetTier = e.Tier;
            r.BudgetCapUsd = e.CapUsd;
            r.BudgetCapTokens = e.CapTokens;
        }, ct);
    }

    /// <summary>p0413: the stated work shape + its reason, so the run view can answer
    /// "why did this ticket get this process".</summary>
    public Task ApplyShapeAsync(IUnitOfWork uow, RunWorkShapeResolvedEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        return UpdateAsync(uow, e.RunId, r =>
        {
            r.WorkShape = e.Shape;
            r.WorkShapeReason = e.Reason;
        }, ct);
    }

    private static async Task UpdateAsync(
        IUnitOfWork uow, string runId, Action<Run> mutate, CancellationToken ct)
    {
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        mutate(run);
        await uow.SaveChangesAsync(ct);
    }
}
