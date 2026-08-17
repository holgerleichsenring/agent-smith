using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0423: writes a run's events straight into the relational store, with no Redis in
/// between.
/// <para>
/// The server's path is stream-then-project, because many producers feed one writer. A
/// CLI run is a single process that IS the only producer, and it had no publisher at all
/// — <c>NoOpEventPublisher</c>, so twelve hours of live debugging ran against a run
/// database of zero bytes and every question cost another run. This is the same
/// projector, called directly.
/// </para>
/// <para>
/// Recording NEVER fails a run. A broken diary is a lost diagnosis; a run aborted by its
/// own diary is a lost run.
/// </para>
/// </summary>
public sealed class ProjectingEventPublisher(
    RunDbProjector projector,
    ILogger<ProjectingEventPublisher> logger) : IEventPublisher
{
    public async Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await projector.ProjectAsync(runEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Could not record {EventType} for run {RunId} — the run continues unrecorded.",
                runEvent.Type, runEvent.RunId);
        }
    }
}
