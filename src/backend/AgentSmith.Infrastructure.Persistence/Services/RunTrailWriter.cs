using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-08-25-61f1: writes a drained batch of trail rows insert-if-absent, per row. The store holds one
/// trail row per (run, position) and the buffer drained this batch out of memory before the write — so a
/// batch that THREW on a duplicate would be gone, unretried. Refusing must not mean losing: the positions
/// the store already holds are dropped from the batch and the rest are written.
/// <para>
/// 2026-10-26-0f19a: another writer can record a position between this one's read and its write. The
/// batch is then read and written once more, in a fresh scope, and the second pass skips those rows.
/// </para>
/// </summary>
public sealed class RunTrailWriter(IServiceScopeFactory scopeFactory)
{
    public async Task WriteAsync(
        string runId, IReadOnlyList<(long Seq, AgentSmith.Contracts.Events.RunEvent Event)> events, CancellationToken ct)
    {
        try { await WriteAbsentAsync(runId, events, ct); }
        catch (DbUpdateException) { await WriteAbsentAsync(runId, events, ct); }
    }

    private async Task WriteAbsentAsync(
        string runId, IReadOnlyList<(long Seq, AgentSmith.Contracts.Events.RunEvent Event)> events, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var recorded = await RecordedSeqsAsync(uow, runId, [.. events.Select(e => e.Seq)], ct);
        foreach (var (seq, ev) in events.Where(e => !recorded.Contains(e.Seq)))
            uow.Add(RunTrailRowMapper.Map(runId, seq, ev));
        await uow.SaveChangesAsync(ct);
    }

    private static async Task<HashSet<long>> RecordedSeqsAsync(
        IUnitOfWork uow, string runId, IReadOnlyList<long> seqs, CancellationToken ct) =>
        [.. await uow.Set<Entities.RunEvent>().AsNoTracking()
            .Where(e => e.RunId == runId && seqs.Contains(e.Seq))
            .Select(e => e.Seq).ToListAsync(ct)];
}
