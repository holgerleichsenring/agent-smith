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
            if (!result.TimedOut && result.ExitCode == 0) return result;
            if (attempt == MaxAttempts || cancellationToken.IsCancellationRequested) return result;

            var pause = options.RetryPause * attempt * attempt;
            logger.LogWarning(
                "External worker failed ({Reason}) on attempt {Attempt}/{Max}; retrying in {Pause:F0}s",
                result.TimedOut ? "timeout" : $"exit {result.ExitCode}",
                attempt, MaxAttempts, pause.TotalSeconds);
            await Task.Delay(pause, cancellationToken);
        }
        return result;
    }
}
