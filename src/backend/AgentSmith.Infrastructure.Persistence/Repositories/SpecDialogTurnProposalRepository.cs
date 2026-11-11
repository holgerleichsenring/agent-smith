using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-10-d854c: each design turn's own proposal, one row per run id. A write replaces the run's row, so a draft
/// under review is overwritten by the next one and finally by the turn's proposal; a write that loses the insert race
/// to another writer of the same run updates the row that writer made.
/// </summary>
public sealed class SpecDialogTurnProposalRepository(IUnitOfWork unitOfWork)
{
    public async Task WriteAsync(string sessionId, string runId, string proposalJson, CancellationToken ct)
    {
        if (await UpdateAsync(runId, proposalJson, ct) > 0) return;
        var added = unitOfWork.Add(new SpecDialogTurnProposal { SessionId = sessionId, RunId = runId, ProposalJson = proposalJson });
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            added.State = EntityState.Detached; // the other writer's row stands; this one is not retried by a later save
            await UpdateAsync(runId, proposalJson, ct);
        }
    }

    /// <summary>The conversation's turn proposals by run id.</summary>
    public async Task<IReadOnlyDictionary<string, string>> ReadBySessionAsync(string sessionId, CancellationToken ct) =>
        await unitOfWork.Set<SpecDialogTurnProposal>().AsNoTracking()
            .Where(p => p.SessionId == sessionId)
            .ToDictionaryAsync(p => p.RunId, p => p.ProposalJson, StringComparer.Ordinal, ct);

    public Task<int> DeleteBySessionAsync(string sessionId, CancellationToken ct) =>
        unitOfWork.Set<SpecDialogTurnProposal>().Where(p => p.SessionId == sessionId).ExecuteDeleteAsync(ct);

    private Task<int> UpdateAsync(string runId, string proposalJson, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return unitOfWork.Set<SpecDialogTurnProposal>().Where(p => p.RunId == runId).ExecuteUpdateAsync(
            row => row.SetProperty(p => p.ProposalJson, proposalJson).SetProperty(p => p.UpdatedAt, now), ct);
    }
}
