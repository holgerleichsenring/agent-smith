using System.Diagnostics;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

internal sealed class StepExecutor(
    IProcessRunner runner,
    FileStepHandler fileHandler,
    GrepStepHandler grepHandler,
    DirectoryTreeStepHandler treeHandler,
    ILogger<StepExecutor> logger) : IStepExecutor
{
    public Task<StepResult> ExecuteAsync(
        Step step,
        Func<IReadOnlyList<StepEvent>, Task> onEvents,
        CancellationToken cancellationToken)
    {
        return step.Kind switch
        {
            StepKind.Run => RunCommandAsync(step, onEvents, cancellationToken),
            StepKind.ReadFile or StepKind.WriteFile or StepKind.ListFiles
                => fileHandler.HandleAsync(step, onEvents, cancellationToken),
            StepKind.Grep => grepHandler.HandleAsync(step, onEvents, cancellationToken),
            StepKind.DirectoryTree => treeHandler.HandleAsync(step, onEvents, cancellationToken),
            _ => throw new ArgumentException(
                $"StepExecutor does not handle Kind={step.Kind}", nameof(step))
        };
    }

    private async Task<StepResult> RunCommandAsync(
        Step step,
        Func<IReadOnlyList<StepEvent>, Task> onEvents,
        CancellationToken cancellationToken)
    {
        await using var batcher = new OutputBatcher(
            OutputBatcher.DefaultThresholdCount,
            OutputBatcher.DefaultFlushInterval,
            onEvents);

        var displayCommand = DisplayCommand(step);
        var shortStepId = ShortStepId(step.StepId);
        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug("Step {StepId} starting `{Command}`", shortStepId, displayCommand);
        batcher.Add(MakeEvent(step.StepId, StepEventKind.Started, step.Command!));

        var outcome = await runner.RunAsync(step,
            (kind, line) => batcher.Add(MakeEvent(step.StepId, kind, line)),
            cancellationToken);

        batcher.Add(MakeEvent(step.StepId, StepEventKind.Completed,
            $"exit={outcome.ExitCode} timedOut={outcome.TimedOut}"));
        var elapsedMs = (long)stopwatch.Elapsed.TotalMilliseconds;
        if (outcome.TimedOut)
            logger.LogWarning("Step {StepId} `{Command}` → TIMED OUT after {Ms}ms",
                shortStepId, Truncate(displayCommand, 100), elapsedMs);
        else if (outcome.ExitCode != 0)
            logger.LogWarning("Step {StepId} `{Command}` → exit={ExitCode} in {Ms}ms",
                shortStepId, Truncate(displayCommand, 100), outcome.ExitCode, elapsedMs);
        else
            logger.LogInformation("Step {StepId} `{Command}` → exit=0 in {Ms}ms",
                shortStepId, Truncate(displayCommand, 100), elapsedMs);

        return new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId,
            outcome.ExitCode, outcome.TimedOut,
            stopwatch.Elapsed.TotalSeconds, outcome.ErrorMessage);
    }

    private static string DisplayCommand(Step step)
    {
        // Shell-invocation idiom: Command is the interpreter, Args[1] is the
        // actual command after `-c`. Surface the meaningful payload so the log
        // line tells the operator what ran instead of always saying '/bin/sh'.
        var cmd = step.Command;
        if (cmd is null) return string.Empty;
        if ((cmd == "/bin/sh" || cmd == "/bin/bash" || cmd == "sh" || cmd == "bash")
            && step.Args is { Count: >= 2 } args && args[0] == "-c")
            return args[1];
        return step.Args is { Count: > 0 } more
            ? $"{cmd} {string.Join(' ', more)}"
            : cmd;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static string ShortStepId(Guid id) => id.ToString("N")[..8];

    private static StepEvent MakeEvent(Guid stepId, StepEventKind kind, string line) =>
        new(StepEvent.CurrentSchemaVersion, stepId, kind, line, DateTimeOffset.UtcNow);
}
