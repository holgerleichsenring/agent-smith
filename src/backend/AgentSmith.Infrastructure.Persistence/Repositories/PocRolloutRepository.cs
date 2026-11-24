using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-11-23-c81ec: the rows a PoC run's rollout follow reads and writes — the run's start, the revisions'
/// rollout states and the Poc state, written with the revision's outcome in one transaction.</summary>
public sealed class PocRolloutRepository(IUnitOfWork unitOfWork)
{
    public async Task<DateTimeOffset?> RunStartedAtAsync(string runId, CancellationToken ct)
    {
        var started = await unitOfWork.Set<Run>().AsNoTracking().Where(r => r.Id == runId).Select(r => (DateTimeOffset?)r.StartedAt)
            .FirstOrDefaultAsync(ct);
        return started;
    }

    /// <summary>The revision a run took in (its RunId), else the PoC's current revision — what the Rollout page shows.</summary>
    public async Task<PocRevision?> RevisionOfRunAsync(Poc poc, string runId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(poc);
        var revisions = unitOfWork.Set<PocRevision>().AsNoTracking().Where(r => r.PocId == poc.Id);
        return await revisions.Where(r => r.RunId == runId).OrderByDescending(r => r.Number).FirstOrDefaultAsync(ct)
            ?? await revisions.Where(r => r.Number == poc.Revision).FirstOrDefaultAsync(ct);
    }

    public Task<bool> HasHealthyRolloutAsync(long pocId, CancellationToken ct) =>
        unitOfWork.Set<PocRevision>().AsNoTracking().AnyAsync(r => r.PocId == pocId && r.RolloutState == PocRolloutStates.Healthy, ct);

    public Task RecordCommitAsync(long pocId, int revision, string commitSha, CancellationToken ct) =>
        unitOfWork.Set<PocRevision>().Where(r => r.PocId == pocId && r.Number == revision)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.RolloutCommitSha, commitSha), ct);

    public async Task RecordOutcomeAsync(PocRolloutOutcome outcome, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        if (outcome.RolloutState is { } rollout) // 2026-11-23-c81ed: a stop, start or apply leaves it untouched
            await unitOfWork.Set<PocRevision>().Where(r => r.PocId == outcome.PocId && r.Number == outcome.Revision)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.RolloutState, rollout)
                    .SetProperty(r => r.RolloutAtTicks, outcome.At.UtcTicks), ct);
        if (outcome.PocState is { } state)
            await unitOfWork.Set<Poc>().Where(p => p.Id == outcome.PocId).ExecuteUpdateAsync(set => set.SetProperty(p => p.State, state), ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>2026-11-23-c81ed: a settled delete — the PoC deleted and its LiveName null in one update, so 933da's unique
    /// (Target, LiveName) frees the name and the host made from it.</summary>
    public Task RecordDeletedAsync(long pocId, CancellationToken ct) =>
        unitOfWork.Set<Poc>().Where(p => p.Id == pocId).ExecuteUpdateAsync(set => set
            .SetProperty(p => p.State, PocStates.Deleted).SetProperty(p => p.LiveName, (string?)null), ct);
}
