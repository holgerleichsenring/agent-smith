using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-08-25-61f1: answers whether a run event's row is already in a table, by the
/// event's trail position. Every projection that INSERTS asks before it inserts, so a
/// second projection of the same event is a no-op the writer observes rather than a
/// constraint violation it loses a whole unit of work to.
/// <para>
/// An event with no position — the terminal reconciler applies one outside the drain —
/// is never suppressed: absent an identity there is nothing to compare, and refusing on
/// a guess would drop a real record.
/// </para>
/// </summary>
public sealed class ProjectedEventRecords
{
    public async Task<bool> HoldsAsync<T>(
        IUnitOfWork uow, string runId, long? eventSeq, CancellationToken ct) where T : class
    {
        if (eventSeq is not { } seq) return false;
        return await uow.Set<T>().AsNoTracking().AnyAsync(
            row => EF.Property<string>(row, "RunId") == runId
                   && EF.Property<long?>(row, "EventSeq") == seq, ct);
    }
}
