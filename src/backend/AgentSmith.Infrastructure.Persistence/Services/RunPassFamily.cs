using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59f: the passes family — each master pass's end and the ledger transitions made inside
/// the passes, the rows a spec's passes are read from.
/// </summary>
public sealed class RunPassFamily(RunPassProjection passes, RunLedgerTransitionProjection transitions) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } =
        [typeof(MasterPassEndedEvent), typeof(LedgerTransitionsRecordedEvent)];

    public Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct) => runEvent switch
    {
        MasterPassEndedEvent e => passes.ApplyAsync(uow, e, eventSeq, ct),
        LedgerTransitionsRecordedEvent e => transitions.ApplyAsync(uow, e, eventSeq, ct),
        _ => Task.CompletedTask,
    };
}
