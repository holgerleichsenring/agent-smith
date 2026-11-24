using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-c81ed: a lifecycle act's GitOps commit becomes the PoC's audit entry — stopped, started,
/// rolled-back, deleted, or deployed with the reason 'applied' — once per run and act: the existence check on (RunId, Act)
/// comes first, so a retry's second event and a replay insert nothing, and 933da's unique (RunId, Act) index backs it.</summary>
public sealed class PocActedProjection : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } = [typeof(PocActedEvent)];

    public async Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uow);
        if (runEvent is not PocActedEvent e || !PocActKinds.IsAct(e.Act)) return;
        var act = PocActKinds.AuditActOf(e.Act);
        if (await uow.Set<PocAuditEntry>().AnyAsync(a => a.RunId == e.RunId && a.Act == act, ct)) return;
        uow.Add(new PocAuditEntry
        {
            PocId = e.PocId, AtTicks = e.Timestamp.UtcTicks, Actor = e.Actor, Act = act, RunId = e.RunId, Reason = Reason(e),
        });
        await uow.SaveChangesAsync(ct);
    }

    private static string? Reason(PocActedEvent e)
    {
        var parts = new[] { e.Reason, e.Revision is { } n ? $"revision {n}" : null, e.CommitSha is { } sha ? $"commit {sha}" : null };
        var said = string.Join(", ", parts.Where(p => p is not null));
        return said.Length == 0 ? null : said;
    }
}
