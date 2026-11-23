using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: tells the server a revision was validated, on the run's event stream — how a step reaches
/// the database. A run without a run id (a CLI run) has nothing to record it against, so nothing is published.</summary>
public sealed class PocImportedPublisher(IEventPublisher events, TimeProvider clock)
{
    public async Task PublishAsync(PipelineContext pipeline, long pocId, int revision, string? actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        await events.PublishAsync(new PocImportedEvent(runId, clock.GetUtcNow(), pocId, revision, actor ?? string.Empty), ct);
    }
}
