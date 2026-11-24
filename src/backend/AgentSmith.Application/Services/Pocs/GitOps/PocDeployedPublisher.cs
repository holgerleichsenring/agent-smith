using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: tells the server a poc-deploy run's GitOps commit ended well, on the run's event stream. A
/// run without a run id (a CLI run) has nothing to record it against, so nothing is published.</summary>
public sealed class PocDeployedPublisher(IEventPublisher events, TimeProvider clock)
{
    public async Task PublishAsync(PipelineContext pipeline, long pocId, PocDesiredState desired, string? commitSha, string? actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(desired);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        await events.PublishAsync(new PocDeployedEvent(
            runId, clock.GetUtcNow(), pocId, desired.Revision, desired.ImageDigest, commitSha, actor ?? string.Empty), ct);
    }
}
