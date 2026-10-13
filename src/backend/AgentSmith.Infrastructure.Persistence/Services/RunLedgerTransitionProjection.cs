using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59f: one <see cref="RunLedgerTransition"/> row per transitions event, with the spec and
/// master pass the publish path stamped on it — written once per trail position.
/// </summary>
public sealed class RunLedgerTransitionProjection(ProjectedEventRecords records)
{
    public async Task ApplyAsync(IUnitOfWork uow, LedgerTransitionsRecordedEvent e, long? eventSeq, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (await records.HoldsAsync<RunLedgerTransition>(uow, e.RunId, eventSeq, ct)) return;
        uow.Add(new RunLedgerTransition
        {
            RunId = e.RunId, EventSeq = eventSeq, SpecId = e.OriginSpecId, MasterPass = e.OriginMasterPass,
            TransitionsJson = e.TransitionsJson, At = e.Timestamp,
        });
        await uow.SaveChangesAsync(ct);
    }
}
