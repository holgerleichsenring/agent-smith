using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Services;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0367: <see cref="IRunEventPersistence"/> backed by the DB projector. The
/// projector batches the raw event trail (RunTrailBuffer) and applies typed run
/// facts, so tool-call writes accumulate rather than hitting one insert per event.
/// The projector is optional — null when relational persistence is off, making
/// this a no-op pass-through.
/// </summary>
public sealed class RunDbEventPersistence(RunDbProjector? projector) : IRunEventPersistence
{
    public Task PersistAsync(RunEvent runEvent, CancellationToken cancellationToken) =>
        projector is null ? Task.CompletedTask : projector.ProjectAsync(runEvent, cancellationToken);
}
