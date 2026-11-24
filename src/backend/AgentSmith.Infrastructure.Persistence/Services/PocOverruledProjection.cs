using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-933dc: a lifted block becomes the PoC's 'overruled' audit entry — who, why (the reason, cut to the
/// column's width only if a caller bypassed the route's bound) and which findings (FindingIdsJson) — once per run.</summary>
public sealed class PocOverruledProjection : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } = [typeof(PocOverruledEvent)];

    public async Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uow);
        if (runEvent is not PocOverruledEvent e) return;
        if (await uow.Set<PocAuditEntry>().AnyAsync(a => a.RunId == e.RunId && a.Act == PocActs.Overruled, ct)) return;
        uow.Add(new PocAuditEntry
        {
            PocId = e.PocId, AtTicks = e.Timestamp.UtcTicks, Actor = e.Actor, Act = PocActs.Overruled, RunId = e.RunId,
            Reason = Bounded(e.Reason), FindingIdsJson = System.Text.Json.JsonSerializer.Serialize(e.FindingIds),
        });
        await uow.SaveChangesAsync(ct);
    }

    private static string Bounded(string text) =>
        text.Length <= Configurations.PocAuditEntryConfiguration.ReasonLength ? text : text[..Configurations.PocAuditEntryConfiguration.ReasonLength];
}
