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
        WorkerPrompt prompt, ExternalWorkerCliOptions options, CancellationToken cancellationToken)
    {
        // 2026-11-17-166ae: a file, not --system-prompt — Linux caps one argument at 128 KB, and the system part of a
        // coding call is larger. Written per call and removed after it, so concurrent calls never share one.
        using var systemFile = options.SystemPromptAsFile ? await WorkerSystemPromptFile.WriteAsync(prompt.System, logger) : null;
        // 2026-11-17-d6f5: the input as blocks under the conversation's session id, so the previous call's are read.
        var blocks = systemFile is not null && options.InputAsBlocks;
        var stdin = systemFile is null ? prompt.Text : blocks ? WorkerStreamInput.Line(prompt.InputBlocks) : prompt.Input;
        string[] extra = systemFile is null ? []
            : blocks ? ["--system-prompt-file", systemFile.Path, "--session-id", prompt.ConversationId]
            : ["--system-prompt-file", systemFile.Path];
        return await RunProcessAsync(stdin, extra, options, cancellationToken);
    }

    private async Task<WorkerProcessResult> RunProcessAsync(
        string stdin, IReadOnlyList<string> extra, ExternalWorkerCliOptions options, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = BuildStartInfo(options, extra) };
        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug("Invoking external worker: {Binary} {Args}",
            options.Binary, string.Join(' ', options.Arguments));
        process.Start();

        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        await WritePromptAsync(process, stdin);

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

    private static ProcessStartInfo BuildStartInfo(ExternalWorkerCliOptions options, IReadOnlyList<string> extra)
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
        foreach (var argument in options.Arguments.Concat(extra)) info.ArgumentList.Add(argument);
        foreach (var (name, value) in options.Environment) info.Environment[name] = value;
        return info;
    }
}
