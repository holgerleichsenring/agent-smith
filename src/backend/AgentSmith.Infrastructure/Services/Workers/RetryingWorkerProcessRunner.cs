using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0419: re-asks a worker whose PROCESS died.
/// <para>
/// Run c96d had two phases verified green — build and test, both repositories — when a
/// single <c>claude -p</c> exited 1 after 2.8 seconds. There was no retry anywhere on
/// this path, so one hiccup in one call recorded the whole ticket as FAILED and threw
/// away 45 minutes of correct work.
/// </para>
/// <para>
/// Only process failures are retried. A worker that ANSWERED is never asked twice,
/// whatever it answered: re-asking would double-spend the model and could double-apply
/// a tool call it already returned. Deciding whether an answer is usable stays with the
/// caller — this decorator's whole subject is the process.
/// </para>
/// <para>
/// p0426: a process that exits 0 having written NOTHING did not answer either, so it is
/// asked again. Run 27 lost eleven minutes of verified work to a single silent call that
/// nothing retried, because "exit 0" looked like success.
/// </para>
/// </summary>
public sealed class RetryingWorkerProcessRunner(
    IWorkerProcessRunner inner, ILogger<RetryingWorkerProcessRunner> logger)
    : IWorkerProcessRunner
{
    private const int MaxAttempts = 3;

    public async Task<WorkerProcessResult> RunAsync(
        string prompt, ExternalWorkerCliOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        WorkerProcessResult result = null!;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            result = await inner.RunAsync(prompt, options, cancellationToken);
            if (Answered(result)) return result;
            if (attempt == MaxAttempts || cancellationToken.IsCancellationRequested) return result;

            var pause = options.RetryPause * attempt * attempt;
            logger.LogWarning(
                "External worker failed ({Reason}) on attempt {Attempt}/{Max}; retrying in {Pause:F0}s",
                Reason(result),
                attempt, MaxAttempts, pause.TotalSeconds);
            await Task.Delay(pause, cancellationToken);
        }
        return result;
    }

    // 2026-09-01-b0d7: on the ANSWER, never on raw stdout. A structured result is never
    // empty, so reading stdout here would retire the silence check the moment an agent
    // opts into it. A CLI that REPORTED an error answered — badly, but once; re-asking a
    // worker that already spoke is what this decorator exists not to do.
    private static bool Answered(WorkerProcessResult result) =>
        !result.TimedOut && result.ExitCode == 0
        && (result.Envelope?.FailureReason is not null
            || !string.IsNullOrWhiteSpace(result.AnswerText));

    private static string Reason(WorkerProcessResult result) => result.TimedOut
        ? "timeout"
        : result.ExitCode != 0 ? $"exit {result.ExitCode}" : "answered nothing";
}
