using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: invokes an external agent CLI (Claude Code's <c>claude -p</c> by default) once
/// per model call. The prompt goes in on stdin — a full conversation with tool schemas
/// exceeds any command line — and the answer comes back on stdout. Unattended by
/// construction: nothing polls, nothing waits for a human, the run drives the CLI.
/// </summary>
public sealed class AgentCliWorkerProcessRunner(ILogger<AgentCliWorkerProcessRunner> logger)
    : IWorkerProcessRunner
{
    public async Task<WorkerProcessResult> RunAsync(
        string prompt, ExternalWorkerCliOptions options, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = BuildStartInfo(options) };
        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug("Invoking external worker: {Binary} {Args}",
            options.Binary, string.Join(' ', options.Arguments));
        process.Start();

        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        await WritePromptAsync(process, prompt);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);
        var timedOut = await WaitAsync(process, timeout.Token, cancellationToken);
        stopwatch.Stop();

        return new WorkerProcessResult(
            timedOut ? -1 : process.ExitCode,
            timedOut ? string.Empty : await stdout,
            timedOut ? string.Empty : await stderr,
            stopwatch.Elapsed,
            timedOut);
    }

    private static async Task WritePromptAsync(Process process, string prompt)
    {
        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();
    }

    // Returns true when the wait ended on the timeout rather than on the process exiting.
    // An operator cancel is NOT a timeout: it propagates so the run stops as asked.
    private async Task<bool> WaitAsync(Process process, CancellationToken timeout, CancellationToken run)
    {
        try
        {
            await process.WaitForExitAsync(timeout);
            return false;
        }
        catch (OperationCanceledException) when (!run.IsCancellationRequested)
        {
            logger.LogWarning("External worker exceeded its per-call timeout; killing the process tree");
            Kill(process);
            return true;
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }
    }

    private void Kill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogDebug(ex, "External worker process had already exited when killed");
        }
    }

    private static ProcessStartInfo BuildStartInfo(ExternalWorkerCliOptions options)
    {
        var info = new ProcessStartInfo
        {
            FileName = options.Binary,
            WorkingDirectory = options.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in options.Arguments) info.ArgumentList.Add(argument);
        return info;
    }
}
