using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59d: the spec family — p0466's spec row, the spec it executed and (2026-09-17-042eh)
/// its review; 2026-10-13-9a59e: the published cut and the not_started rows it stands up.
/// </summary>
public sealed class RunSpecFamily(RunSpecProjection specs, RunSeriesCutProjection cuts) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } =
    [
        typeof(SpecStateChangedEvent), typeof(SpecRecordedEvent), typeof(SpecReviewedEvent),
        typeof(SpecSetPublishedEvent),
    ];

    public Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct) => runEvent switch
    {
        SpecStateChangedEvent e => specs.ApplyStateAsync(uow, e, ct),
        SpecRecordedEvent e => specs.ApplyRecordAsync(uow, e, ct),
        SpecReviewedEvent e => specs.ApplyReviewAsync(uow, e, ct),
        SpecSetPublishedEvent e => cuts.ApplyAsync(uow, e, eventSeq, ct),
        _ => Task.CompletedTask,
    };
}
