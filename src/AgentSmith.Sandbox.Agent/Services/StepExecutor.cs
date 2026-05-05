using System.Diagnostics;
using AgentSmith.Sandbox.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

internal sealed class StepExecutor(IProcessRunner runner, ILogger<StepExecutor> logger) : IStepExecutor
{
    public async Task<StepResult> ExecuteAsync(
        Step step,
        Func<IReadOnlyList<StepEvent>, Task> onEvents,
        CancellationToken cancellationToken)
    {
        EnsureRunStep(step);
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

    private static void EnsureRunStep(Step step)
    {
        if (step.Kind != StepKind.Run)
            throw new ArgumentException("StepExecutor only handles Kind=Run", nameof(step));
        if (string.IsNullOrEmpty(step.Command))
            throw new ArgumentException("Step.Command must be set for Kind=Run", nameof(step));
    }

    private static StepEvent MakeEvent(Guid stepId, StepEventKind kind, string line) =>
        new(StepEvent.CurrentSchemaVersion, stepId, kind, line, DateTimeOffset.UtcNow);
}
