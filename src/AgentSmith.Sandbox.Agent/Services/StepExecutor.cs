using System.Diagnostics;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

internal sealed class StepExecutor(
    IProcessRunner runner,
    FileStepHandler fileHandler,
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

        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Step {StepId} starting: {Command}", step.StepId, step.Command);
        batcher.Add(MakeEvent(step.StepId, StepEventKind.Started, step.Command!));

        var outcome = await runner.RunAsync(step,
            (kind, line) => batcher.Add(MakeEvent(step.StepId, kind, line)),
            cancellationToken);

        batcher.Add(MakeEvent(step.StepId, StepEventKind.Completed,
            $"exit={outcome.ExitCode} timedOut={outcome.TimedOut}"));
        logger.LogInformation("Step {StepId} finished: exit={ExitCode} timedOut={TimedOut} duration={Duration:F2}s",
            step.StepId, outcome.ExitCode, outcome.TimedOut, stopwatch.Elapsed.TotalSeconds);

        return new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId,
            outcome.ExitCode, outcome.TimedOut,
            stopwatch.Elapsed.TotalSeconds, outcome.ErrorMessage);
    }

    private static StepEvent MakeEvent(Guid stepId, StepEventKind kind, string line) =>
        new(StepEvent.CurrentSchemaVersion, stepId, kind, line, DateTimeOffset.UtcNow);
}
