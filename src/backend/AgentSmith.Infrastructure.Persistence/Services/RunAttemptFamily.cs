using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59d: the attempt family — why the attempt began (the fetched ticket) and what it
/// worked from (the published spec set), on the run's one RunAttempt row.
/// </summary>
public sealed class RunAttemptFamily(RunAttemptProjection attempts) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } = [typeof(TicketFetchedEvent), typeof(SpecSetPublishedEvent)];

    public Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct) => runEvent switch
    {
        TicketFetchedEvent e => attempts.ApplyFetchAsync(uow, e, ct),
        SpecSetPublishedEvent e => attempts.ApplyPublishAsync(uow, e, ct),
        _ => Task.CompletedTask,
    };
}
