using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Progress;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0356: publishes the current progress ledger onto the event stream on every
/// update_progress replace — the durable ledger is written MID-RUN, not only at
/// WriteRunResult, so a reaped/crashed run leaves a resumable checklist behind
/// (and the dashboard sees the checklist live). Publishes are chained so they
/// land in replace order, and they are fire-and-forget relative to the tool
/// call: a lost flush is repaired by the next one / the run-end snapshot.
/// </summary>
public sealed class ProgressLedgerFlusher(IEventPublisher events, string runId, ILogger logger)
{
    private readonly object _gate = new();
    private Task _chain = Task.CompletedTask;

    public void Flush(ProgressLedger ledger)
    {
        var json = RunStorySnapshotBuilder.BuildLedgerJson(ledger);
        if (json is null) return;
        lock (_gate)
            _chain = _chain.ContinueWith(
                _ => PublishAsync(json),
                CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
    }

    /// <summary>The tail of the ordered publish chain — awaitable in tests.</summary>
    public Task Completion { get { lock (_gate) return _chain; } }

    private async Task PublishAsync(string ledgerJson)
    {
        try
        {
            // AcceptanceJson stays null: the applier's null-coalescing keeps any
            // previously persisted acceptance snapshot untouched.
            await events.PublishAsync(
                new RunStoryRecordedEvent(runId, ledgerJson, AcceptanceJson: null, DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex,
                "Mid-run ledger flush failed for {RunId} — the next flush / run-end snapshot repairs it", runId);
        }
    }
}
