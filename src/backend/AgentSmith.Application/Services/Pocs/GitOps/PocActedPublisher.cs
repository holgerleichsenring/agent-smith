using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81ed: tells the server a lifecycle act's GitOps commit ended well, on the run's event stream — the
/// act's audit entry is projected from it. An apply is recorded as 'deployed' with the reason 'applied'. A run without a run
/// id (a CLI run) has nothing to record it against, so nothing is published.</summary>
public sealed class PocActedPublisher(IEventPublisher events, TimeProvider clock)
{
    public const string AppliedReason = "applied";

    public async Task PublishAsync(
        PipelineContext pipeline, long pocId, string act, int? revision, string? commitSha, string? actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        await events.PublishAsync(new PocActedEvent(runId, clock.GetUtcNow(), pocId, act, actor ?? string.Empty,
            act == PocActKinds.Apply ? AppliedReason : null, revision, commitSha), ct);
    }
}
