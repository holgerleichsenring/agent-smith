using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Progress;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0356: publishes the current progress ledger onto the event stream on every
/// update_progress replace — the durable ledger is written MID-RUN, not only at
/// WriteRunResult, so a reaped/crashed run leaves a resumable checklist behind
/// (and the dashboard sees the checklist live). The publish is AWAITED by the
/// tool call: no flush ever outlives the master handler (a fire-and-forget
/// chain raced dialogue-checkpoint teardown into a disposed SQLite provider).
/// A failed publish is swallowed — the next flush / run-end snapshot repairs it.
/// </summary>
public sealed class ProgressLedgerFlusher(IEventPublisher events, string runId, ILogger logger)
{
    public async Task FlushAsync(ProgressLedger ledger)
    {
        var json = RunStorySnapshotBuilder.BuildLedgerJson(ledger);
        if (json is null) return;
        try
        {
            // AcceptanceJson stays null: the applier's null-coalescing keeps any
            // previously persisted acceptance snapshot untouched.
            await events.PublishAsync(
                new RunStoryRecordedEvent(runId, json, AcceptanceJson: null, DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex,
                "Mid-run ledger flush failed for {RunId} — the next flush / run-end snapshot repairs it", runId);
        }
    }
}
