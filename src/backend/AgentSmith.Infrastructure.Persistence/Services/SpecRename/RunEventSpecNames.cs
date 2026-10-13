using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: RunEvents — the type names stored under an old <see cref="EventType"/>
/// name (codes 79-81 keep their numbers, only the stored name moves), then the payloads of the
/// event types <see cref="EventPayloadRewriter"/> owns. Only rows whose payload says "hase" are
/// opened, which keeps a second pass to an index scan.
/// </summary>
public sealed class RunEventSpecNames(EventPayloadRewriter payloads, RowPages pages) : IStoredSpecNameSurface
{
    public string Name => "RunEvents";

    public async Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        var renamed = 0;
        foreach (var rename in SpecRenameTable.EventTypeNames)
            renamed += await db.RunEvents.Where(e => e.Type == rename.Old)
                .ExecuteUpdateAsync(set => set.SetProperty(e => e.Type, rename.New), ct);
        var types = payloads.TypeNames.ToList();
        var rows = db.RunEvents.Where(e => types.Contains(e.Type) && e.PayloadJson != null && e.PayloadJson.Contains("hase"));
        return renamed + await pages.RewriteAsync(db, rows, e => e.Id, Rewrite, ct);
    }

    private bool Rewrite(Entities.RunEvent row)
    {
        if (payloads.Rewrite(row.Type, row.PayloadJson) is not { } rewritten) return false;
        row.PayloadJson = rewritten;
        return true;
    }
}
