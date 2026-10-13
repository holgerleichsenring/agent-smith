using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-9a59d: announces a placed spec set as <see cref="SpecSetPublishedEvent"/> — the revision
/// and approval the attempt works from, the executed ids carried in and whether the write committed.
/// 2026-10-13-9a59e: with the cut — the set's key, its hand-back case and every spec as published.
/// Best-effort: a lost announcement costs the ticket page a line, never the run.
/// </summary>
public sealed class SpecSetAnnouncer(IEventPublisher events, ILogger<SpecSetAnnouncer> logger)
{
    public Task AnnounceAsync(
        PipelineContext pipeline, SpecSet set, bool committed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        return PublishAsync(pipeline, set.Key, runId => Build(runId, set, committed), cancellationToken);
    }

    /// <summary>
    /// 2026-10-13-9a59e: a hand-back that never reached the publisher — a scope refusal, a missing
    /// spec — announced as a cut with the ticket key, no specs, its case and no revision. A set the
    /// publisher already handed back was announced there and is not announced twice.
    /// </summary>
    public Task AnnounceHandbackAsync(
        PipelineContext pipeline, string ticketKey, SpecHandback handback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) && set is { IsHandedBack: true })
            return Task.CompletedTask;
        return PublishAsync(pipeline, ticketKey, runId => new SpecSetPublishedEvent(
            runId, DateTimeOffset.UtcNow, SetKey: ticketKey,
            HandbackCase: SpecCutContent.CaseOf(handback), Specs: []), cancellationToken);
    }

    private async Task PublishAsync(
        PipelineContext pipeline, string key, Func<string, SpecSetPublishedEvent> build, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        try
        {
            await events.PublishAsync(build(runId), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Announcing spec set {Key} failed", key);
        }
    }

    private static SpecSetPublishedEvent Build(string runId, SpecSet set, bool committed)
    {
        var revision = set.Revisions.Count > 0 ? set.Current : null;
        return new SpecSetPublishedEvent(
            runId, DateTimeOffset.UtcNow,
            revision?.Number, revision?.Cause, set.Series, set.Executed,
            set.Approval?.At, set.Approval?.Conversation, set.Approval?.Principal, committed,
            set.Key, SpecCutContent.CaseOf(set.Handback), SpecCutContent.Of(set));
    }
}
