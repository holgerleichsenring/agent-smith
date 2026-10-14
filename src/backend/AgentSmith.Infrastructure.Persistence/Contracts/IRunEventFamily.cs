using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// 2026-10-13-9a59d: one family of run-event projections — the events it applies, named, and how it
/// applies each. The applier only dispatches; a new projected event is an arm in the family that owns
/// its table, or a family of its own.
/// </summary>
public interface IRunEventFamily
{
    /// <summary>The concrete event types this family applies.</summary>
    IReadOnlyCollection<Type> Events { get; }

    /// <param name="eventSeq">The event's position in its run's trail, or null outside the drain.</param>
    Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct);
}
