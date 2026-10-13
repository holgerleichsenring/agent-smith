using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-9a59d: announces a placed spec set as <see cref="SpecSetPublishedEvent"/> — the revision
/// and approval the attempt works from, the executed ids carried in and whether the write committed.
/// Best-effort: a lost announcement costs the ticket page a line, never the run.
/// </summary>
public sealed class SpecSetAnnouncer(IEventPublisher events, ILogger<SpecSetAnnouncer> logger)
{
    public async Task AnnounceAsync(
        PipelineContext pipeline, SpecSet set, bool committed, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        try
        {
            await events.PublishAsync(Build(runId, set, committed), cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Announcing spec set {Key} failed", set.Key);
        }
    }

    private static SpecSetPublishedEvent Build(string runId, SpecSet set, bool committed)
    {
        var revision = set.Revisions.Count > 0 ? set.Current : null;
        return new SpecSetPublishedEvent(
            runId, DateTimeOffset.UtcNow,
            revision?.Number, revision?.Cause, set.Series, set.Executed,
            set.Approval?.At, set.Approval?.Conversation, set.Approval?.Principal, committed);
    }
}
