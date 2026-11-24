using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-c81eb: a poc-deploy run's GitOps commit becomes the PoC's 'deployed' audit entry — once per run:
/// the existence check on (RunId, Act) comes first, so a retry's second event and a replay insert nothing, and 933da's
/// unique (RunId, Act) index backs it.</summary>
public sealed class PocDeployedProjection : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } = [typeof(PocDeployedEvent)];

    public async Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uow);
        if (runEvent is not PocDeployedEvent e) return;
        if (await uow.Set<PocAuditEntry>().AnyAsync(a => a.RunId == e.RunId && a.Act == PocActs.Deployed, ct)) return;
        uow.Add(new PocAuditEntry
        {
            PocId = e.PocId, AtTicks = e.Timestamp.UtcTicks, Actor = e.Actor, Act = PocActs.Deployed, RunId = e.RunId,
            Reason = e.CommitSha is null ? $"revision {e.Revision}" : $"revision {e.Revision}, commit {e.CommitSha}",
        });
        await uow.SaveChangesAsync(ct);
    }
}
