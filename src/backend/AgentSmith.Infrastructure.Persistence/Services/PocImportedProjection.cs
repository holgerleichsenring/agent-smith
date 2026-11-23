using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-933db: a validated revision becomes the PoC's 'imported' audit entry — once per run, so a replayed
/// event inserts nothing.</summary>
public sealed class PocImportedProjection : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } = [typeof(PocImportedEvent)];

    public async Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uow);
        if (runEvent is not PocImportedEvent e) return;
        if (await uow.Set<PocAuditEntry>().AnyAsync(a => a.RunId == e.RunId && a.Act == PocActs.Imported, ct)) return;
        uow.Add(new PocAuditEntry
        {
            PocId = e.PocId, AtTicks = e.Timestamp.UtcTicks, Actor = e.Actor, Act = PocActs.Imported, RunId = e.RunId,
            Reason = $"revision {e.Revision}",
        });
        await uow.SaveChangesAsync(ct);
    }
}
