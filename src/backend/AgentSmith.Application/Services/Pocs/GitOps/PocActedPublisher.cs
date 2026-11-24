using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81ed: tells the server a lifecycle act's GitOps commit ended well, on the run's event stream — the
/// act's audit entry is projected from it. An apply is recorded as 'deployed' with the reason 'applied'. A run without a run
/// id (a CLI run) has nothing to record it against, so nothing is published. 2026-11-23-c81ee: a run that carries an audit
/// act (ContextKeys.PocAuditAct: an expiry or a removal) records it with its reason in place of the act's own; an extension
/// is published by its own step through <see cref="PublishExtendedAsync"/>.</summary>
public sealed class PocActedPublisher(IEventPublisher events, TimeProvider clock)
{
    public const string AppliedReason = "applied";

    public async Task PublishAsync(
        PipelineContext pipeline, long pocId, string act, int? revision, string? commitSha, string? actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        var audit = pipeline.TryGet<PocAuditAct>(ContextKeys.PocAuditAct, out var carried) ? carried : null;
        await events.PublishAsync(new PocActedEvent(runId, clock.GetUtcNow(), pocId, act, actor ?? string.Empty,
            audit?.Reason ?? (act == PocActKinds.Apply ? AppliedReason : null), revision, commitSha, audit?.Act), ct);
    }

    public async Task PublishExtendedAsync(PipelineContext pipeline, long pocId, string? actor, string reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        await events.PublishAsync(new PocActedEvent(runId, clock.GetUtcNow(), pocId, PocActs.Extended, actor ?? string.Empty,
            reason, null, null, PocActs.Extended), ct);
    }
}
