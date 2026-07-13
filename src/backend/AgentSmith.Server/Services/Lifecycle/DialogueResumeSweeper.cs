using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// p0327: the single resume trigger. Every scan it walks the pending
/// checkpoints and (a) resumes those whose durable inbox already holds an
/// answer — this also closes the answer-before-checkpoint race, since both
/// sides are rows by the next tick — and (b) applies the persisted
/// DefaultAnswer when the days-scale deadline elapsed, so the run resumes
/// headless (same timeout contract as the hot wait, longer clock). Runs under
/// the housekeeping leader; scan latency (seconds) is noise against waits
/// measured in hours or days.
/// </summary>
public sealed class DialogueResumeSweeper(
    IServiceProvider services,
    IRunCheckpointStore checkpointStore,
    IDialogueAnswerInbox inbox,
    IRunResumer runResumer,
    TimeProvider timeProvider,
    ILogger<DialogueResumeSweeper> logger)
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(15);

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
        foreach (var checkpoint in await checkpointStore.ListPendingAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            if (await IsRunAbandonedAsync(checkpoint.RunId, ct))
            {
                // Cancelled / force-finished while parked — consume, never resume.
                await checkpointStore.TryMarkResumedAsync(
                    checkpoint.RunId, timeProvider.GetUtcNow(), ct);
                continue;
            }
            if (await TryResumeAsync(checkpoint, ct)) resumed++;
        }
        return resumed;
    }

    private async Task<bool> TryResumeAsync(RunCheckpointRecord checkpoint, CancellationToken ct)
    {
        var answer = await inbox.GetAsync(checkpoint.DialogueJobId, checkpoint.QuestionId, ct)
                     ?? await ApplyDeadlineDefaultAsync(checkpoint, ct);
        if (answer is null) return false; // still waiting, deadline not reached
        return await runResumer.EnqueueResumeAsync(checkpoint, answer, ct);
    }

    // Deadline elapsed → the persisted DefaultAnswer applies, written through
    // the SAME first-wins inbox so a racing real answer beats the default.
    private async Task<DialogAnswer?> ApplyDeadlineDefaultAsync(
        RunCheckpointRecord checkpoint, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        if (now < checkpoint.AnswerDeadlineAt) return null;

        var question = System.Text.Json.JsonSerializer.Deserialize<DialogQuestion>(checkpoint.QuestionJson);
        var fallback = new DialogAnswer(
            checkpoint.QuestionId, question?.DefaultAnswer ?? "", "timeout", now, "system");
        await inbox.TryDeliverAsync(checkpoint.DialogueJobId, fallback, ct);
        logger.LogWarning(
            "Run {RunId} answer deadline elapsed — applying default answer '{Default}' headless",
            checkpoint.RunId, fallback.Answer);
        return await inbox.GetAsync(checkpoint.DialogueJobId, checkpoint.QuestionId, ct);
    }

    // Abandoned = terminal or cancel-requested. A run still 'running' is NOT
    // abandoned: the checkpoint event lands while the executor is unwinding
    // (sandbox teardown), before its RunFinished(waiting_for_input) — that gap
    // must never consume the checkpoint.
    private async Task<bool> IsRunAbandonedAsync(string runId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var run = await scope.ServiceProvider.GetRequiredService<RunRepository>()
            .GetRunDetailAsync(runId, ct);
        if (run is null) return false; // projection lag — retry next scan
        return run.FinishedAt is not null || run.CancelRequested;
    }
}
