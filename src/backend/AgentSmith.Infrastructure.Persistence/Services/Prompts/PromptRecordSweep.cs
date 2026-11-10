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
    // 2026-11-09-0620l: in pages by id, so the sweep holds one page in memory, not the table.
    private const int Page = 1000;

    public async Task<int> PruneAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var deleted = 0;
        var after = string.Empty;
        while (true)
        {
            var page = await unitOfWork.Set<PromptRecord>().AsNoTracking()
                .Where(r => string.Compare(r.Id, after) > 0).OrderBy(r => r.Id).Take(Page)
                .Select(r => new { r.Id, r.CreatedAt }).ToListAsync(ct);
            if (page.Count == 0) return deleted;
            after = page[^1].Id;
            deleted += await DeleteRecordsAsync([.. page.Where(r => r.CreatedAt < cutoff).Select(r => r.Id)], ct);
        }
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
