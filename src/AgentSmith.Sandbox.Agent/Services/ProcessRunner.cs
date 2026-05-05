using System.Diagnostics;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Sandbox.Agent.Services;

internal sealed class ProcessRunner : IProcessRunner
{
    private const int KillGraceSeconds = 10;

    public async Task<ProcessOutcome> RunAsync(
        Step step,
        Action<StepEventKind, string> onLine,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = BuildStartInfo(step) };
        try
        {
            if (!process.Start())
            {
                return new ProcessOutcome(-1, false, "process failed to start");
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new ProcessOutcome(-1, false, $"failed to start '{step.Command}': {ex.Message}");
        }

        var readers = StartReaders(process, onLine, cancellationToken);
        return await WaitForExitAsync(process, readers, step.TimeoutSeconds, cancellationToken);
    }

    private static ProcessStartInfo BuildStartInfo(Step step)
    {
        var info = new ProcessStartInfo
        {
            FileName = step.Command!,
            WorkingDirectory = step.WorkingDirectory ?? "/work",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in step.Args ?? Array.Empty<string>())
        {
            info.ArgumentList.Add(arg);
        }
        foreach (var (key, value) in step.Env ?? new Dictionary<string, string>())
        {
            info.Environment[key] = value;
        }
        return info;
    }

    private static Task[] StartReaders(
        Process process,
        Action<StepEventKind, string> onLine,
        CancellationToken cancellationToken) =>
    [
        ReadLinesAsync(process.StandardOutput, StepEventKind.Stdout, onLine, cancellationToken),
        ReadLinesAsync(process.StandardError, StepEventKind.Stderr, onLine, cancellationToken)
    ];

    private static async Task ReadLinesAsync(
        StreamReader reader,
        StepEventKind kind,
        Action<StepEventKind, string> onLine,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return;
            }
            onLine(kind, line);
        }
    }

    private async Task<ProcessOutcome> WaitForExitAsync(
        Process process,
        Task[] readers,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            await Task.WhenAll(readers);
            return new ProcessOutcome(process.ExitCode, false, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await KillAndDrainAsync(process, readers);
            return new ProcessOutcome(-1, true, $"timed out after {timeoutSeconds}s");
        }
    }

    private static async Task KillAndDrainAsync(Process process, Task[] readers)
    {
        try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(KillGraceSeconds));
        try { await process.WaitForExitAsync(grace.Token); } catch (OperationCanceledException) { }
        try { await Task.WhenAll(readers); } catch (OperationCanceledException) { }
    }
}
