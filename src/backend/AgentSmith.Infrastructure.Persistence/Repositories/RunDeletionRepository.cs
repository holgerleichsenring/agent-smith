using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// p0337: deletes a run and every satellite keyed to it. Run children carry a
/// plain indexed RunId (no FK, no cascade — see AgentSmithDbContext), and the
/// lease / queue entry / checkpoint / expectation / dialogue inbox are separate
/// tables, so a Run-row delete alone would orphan all of them. Every table is
/// cleared in ONE transaction. Ticket-shared satellites (lease, queue entry) are
/// keyed by the RUN id, never by (project, ticket): a re-triggered ticket's newer
/// run must keep its lease when an older run of the same ticket is deleted.
/// </summary>
public sealed class RunDeletionRepository(IUnitOfWork unitOfWork)
{
    public Task<int> DeleteAsync(string runId, CancellationToken ct) =>
        DeleteManyAsync([runId], ct);

    // Bulk "clear terminal runs" — success/failed/cancelled only (FinishedAt set).
    public async Task<int> DeleteTerminalAsync(CancellationToken ct)
    {
        var ids = await unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.FinishedAt != null).Select(r => r.Id).ToListAsync(ct);
        return ids.Count == 0 ? 0 : await DeleteManyAsync(ids, ct);
    }

    private async Task<int> DeleteManyAsync(IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        await DeleteChildrenAsync(ids, ct);
        await DeleteSatellitesAsync(ids, ct);
        var deleted = await unitOfWork.Set<Run>().Where(r => ids.Contains(r.Id)).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
        return deleted;
    }

    private async Task DeleteChildrenAsync(IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        await DeleteByRunIdAsync<RunRepo>(ids, ct);
        await DeleteByRunIdAsync<RunStep>(ids, ct);
        await DeleteByRunIdAsync<RunEvent>(ids, ct);
        await DeleteByRunIdAsync<RunDecision>(ids, ct);
        await DeleteByRunIdAsync<RunLlmCall>(ids, ct);
        await DeleteByRunIdAsync<RunArtifact>(ids, ct);
        await DeleteByRunIdAsync<RunSandbox>(ids, ct);
    }

    private async Task DeleteSatellitesAsync(IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        // The dialogue inbox is keyed by DialogueJobId, reachable only via the
        // checkpoint — clear it before the checkpoint rows go.
        var dialogueJobIds = await unitOfWork.Set<RunCheckpoint>().AsNoTracking()
            .Where(c => ids.Contains(c.RunId)).Select(c => c.DialogueJobId).ToListAsync(ct);
        await unitOfWork.Set<DialogueAnswerEntry>()
            .Where(a => dialogueJobIds.Contains(a.DialogueJobId)).ExecuteDeleteAsync(ct);
        await DeleteByRunIdAsync<RunCheckpoint>(ids, ct);
        await DeleteByRunIdAsync<RunExpectation>(ids, ct);
        await DeleteByRunIdAsync<RunCapacity>(ids, ct); // p0336: release the ledger row
        await unitOfWork.Set<QueuedTicket>()
            .Where(q => q.ReservedRunId != null && ids.Contains(q.ReservedRunId)).ExecuteDeleteAsync(ct);
        await unitOfWork.Set<ActiveRun>()
            .Where(a => a.RunId != null && ids.Contains(a.RunId)).ExecuteDeleteAsync(ct);
    }

    private Task DeleteByRunIdAsync<T>(IReadOnlyCollection<string> ids, CancellationToken ct) where T : class =>
        unitOfWork.Set<T>().Where(x => ids.Contains(EF.Property<string>(x, "RunId"))).ExecuteDeleteAsync(ct);
}
