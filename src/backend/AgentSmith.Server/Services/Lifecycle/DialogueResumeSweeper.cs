using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// p0327: the single resume trigger. Every scan it walks the pending checkpoints and (a)
/// resumes those whose durable inbox already holds an answer — this also closes the
/// answer-before-checkpoint race, since both sides are rows by the next tick — and (b) applies the
/// persisted DefaultAnswer when the days-scale deadline elapsed, so the run resumes headless (same
/// timeout contract as the hot wait, longer clock). Runs under the housekeeping leader; scan
/// latency (seconds) is noise against waits measured in hours or days. 2026-11-27-3532b: the ticket's answer and the default
/// are <see cref="ParkedAnswerFallback"/>'s, which takes neither for a series-led run; each project's series are read once per
/// scan, and one checkpoint that fails is logged and skipped, never stopping the others' resumes.
/// </summary>
public sealed class DialogueResumeSweeper(
    IServiceProvider services,
    IRunCheckpointStore checkpointStore,
    IDialogueAnswerInbox inbox,
    IRunResumer runResumer,
    IParkedTicketDialogue ticket,
    ParkedAnswerFallback fallback,
    Series.SeriesLedRun led,
    UnanswerableParkReporter unanswerableParks,
    SupersededParkCloser supersededParks,
    IRunListNudge runListNudge,
    TimeProvider timeProvider,
    ILogger<DialogueResumeSweeper> logger)
{
    internal static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(15); // 2026-11-17-0207a: the series sweep scans with it

    public async Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("DialogueResumeSweeper started (scan {Scan})", ScanInterval);
        while (!ct.IsCancellationRequested)
        {
            try { await ScanOnceAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Dialogue resume scan failed"); }

            try { await Task.Delay(ScanInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    // Public so tests drive single deterministic scans. Returns resumes enqueued.
    public async Task<int> ScanOnceAsync(CancellationToken ct)
    {
        var resumed = 0;
        await supersededParks.CloseAsync(ct); // 2026-10-16-86e6b: a superseded checkpoint is abandoned, never resumed
        var scan = led.Scan();
        foreach (var checkpoint in await checkpointStore.ListPendingAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            try { if (await SweepAsync(checkpoint, scan, ct)) resumed++; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Resume scan of run {RunId} failed; the other parked runs go on", checkpoint.RunId);
            }
        }
        // a508: a park with no checkpoint has nothing to wait ON, so it is reported rather than waited out.
        await unanswerableParks.ReportAsync(ct);
        return resumed;
    }

    private async Task<bool> SweepAsync(RunCheckpointRecord checkpoint, Series.SeriesLedScan scan, CancellationToken ct)
    {
        if (!await IsRunAbandonedAsync(checkpoint.RunId, ct)) return await TryResumeAsync(checkpoint, scan, ct);
        // Cancelled / force-finished while parked — consume, never resume.
        await checkpointStore.TryMarkResumedAsync(checkpoint.RunId, timeProvider.GetUtcNow(), ct);
        return false;
    }

    private async Task<bool> TryResumeAsync(RunCheckpointRecord checkpoint, Series.SeriesLedScan scan, CancellationToken ct)
    {
        var answer = await inbox.GetAsync(checkpoint.DialogueJobId, checkpoint.QuestionId, ct)
                     ?? await fallback.TakeAsync(checkpoint, scan, ct);
        if (answer is null) return false; // still waiting, deadline not reached
        if (!await runResumer.EnqueueResumeAsync(checkpoint, answer, ct)) return false;
        // 2026-09-22-7c41c: a parked run is outside the set the broadcaster drains, so nothing else
        // tells an open surface to re-read. Best-effort, and at this sole caller, not the resumer.
        try { await runListNudge.RunsChangedAsync(checkpoint.RunId, ct); }
        catch (Exception ex) { logger.LogDebug(ex, "Resume nudge failed for {RunId}", checkpoint.RunId); }

        // p0461: and the board stops saying "waiting for you" over a working run.
        await ticket.MoveToInProgressAsync(checkpoint, ct);
        return true;
    }

    // Abandoned = terminal or cancel-requested. A run still 'running' is NOT: the checkpoint lands while the
    // executor unwinds, before its RunFinished(waiting_for_input) — that gap must never consume the checkpoint.
    private async Task<bool> IsRunAbandonedAsync(string runId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var run = await scope.ServiceProvider.GetRequiredService<RunRepository>()
            .GetRunDetailAsync(runId, ct);
        if (run is null) return false; // projection lag — retry next scan
        return run.FinishedAt is not null || run.CancelRequested;
    }
}
