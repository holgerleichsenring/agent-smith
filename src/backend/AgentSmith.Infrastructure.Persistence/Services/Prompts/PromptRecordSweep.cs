using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Prompts;

/// <summary>
/// 2026-11-09-0620a: removes stored prompts with the run they belong to, or once older than the retention age, and
/// then every block no remaining record holds. A block is shared between calls and runs, so it goes only when nothing
/// names it.
/// </summary>
public sealed class PromptRecordSweep(IUnitOfWork unitOfWork)
{
    public async Task<int> DeleteRunsAsync(IReadOnlyCollection<string> runIds, CancellationToken ct)
    {
        var ids = await unitOfWork.Set<PromptRecord>().AsNoTracking()
            .Where(r => runIds.Contains(r.RunId)).Select(r => r.Id).ToListAsync(ct);
        return await DeleteRecordsAsync(ids, ct);
    }

    // Filtered client-side, as RunRetentionService does: SQLite cannot translate a DateTimeOffset comparison.
    public async Task<int> PruneAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var ids = (await unitOfWork.Set<PromptRecord>().AsNoTracking()
                .Select(r => new { r.Id, r.CreatedAt }).ToListAsync(ct))
            .Where(r => r.CreatedAt < cutoff).Select(r => r.Id).ToList();
        return await DeleteRecordsAsync(ids, ct);
    }

    private async Task<int> DeleteRecordsAsync(List<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return 0;
        await unitOfWork.Set<PromptRecordBlock>().Where(b => ids.Contains(b.RecordId)).ExecuteDeleteAsync(ct);
        var records = await unitOfWork.Set<PromptRecord>().Where(r => ids.Contains(r.Id)).ExecuteDeleteAsync(ct);
        await unitOfWork.Set<PromptBlockRow>()
            .Where(b => !unitOfWork.Set<PromptRecordBlock>().Any(h => h.BlockHash == b.Hash))
            .ExecuteDeleteAsync(ct);
        return records;
    }
}
