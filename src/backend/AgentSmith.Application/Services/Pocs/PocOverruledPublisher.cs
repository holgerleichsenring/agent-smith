using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933dc: tells the server an overrule run lifted a block, on the run's event stream — its projection
/// writes the 'overruled' audit entry. A run without a run id has nothing to record it against.</summary>
public sealed class PocOverruledPublisher(IEventPublisher events, TimeProvider clock)
{
    public async Task PublishAsync(
        PipelineContext pipeline, long pocId, int revision, string stage, string actor, string reason, IReadOnlyList<string> ids,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        await events.PublishAsync(new PocOverruledEvent(runId, clock.GetUtcNow(), pocId, revision, stage, actor, reason, ids), ct);
    }
}
