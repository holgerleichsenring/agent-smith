using AgentSmith.Sandbox.Agent.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

internal sealed class JobLoop(IRedisJobBus bus, IStepExecutor executor, ILogger<JobLoop> logger)
{
    public const int ExitOk = 0;
    public const int ExitIdleTimeout = 2;
    public const int MaxIdleCycles = 5;
    public static readonly TimeSpan IdlePollTimeout = TimeSpan.FromSeconds(60);

    public async Task<int> RunAsync(string jobId, CancellationToken cancellationToken)
    {
        var idleCycles = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var step = await bus.WaitForStepAsync(jobId, IdlePollTimeout, cancellationToken);
            if (step is null)
            {
                if (++idleCycles >= MaxIdleCycles)
                {
                    logger.LogWarning("No step received in {Cycles} idle cycles; exiting", idleCycles);
                    return ExitIdleTimeout;
                }
                logger.LogWarning("No step received in {Timeout}; idle cycle {Cycle}/{Max}",
                    IdlePollTimeout, idleCycles, MaxIdleCycles);
                continue;
            }
            idleCycles = 0;
            if (step.Kind == StepKind.Shutdown)
            {
                logger.LogInformation("Shutdown step received; exiting");
                return ExitOk;
            }
            await ProcessRunStepAsync(jobId, step, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return ExitOk;
    }

    private async Task ProcessRunStepAsync(string jobId, Step step, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(step.Command))
        {
            await PushValidationFailureAsync(jobId, step, cancellationToken);
            return;
        }

        var result = await executor.ExecuteAsync(step,
            batch => { bus.EnqueueEventsBatch(jobId, batch); return Task.CompletedTask; },
            cancellationToken);
        await bus.PushResultAsync(jobId, result, cancellationToken);
    }

    private async Task PushValidationFailureAsync(string jobId, Step step, CancellationToken cancellationToken)
    {
        logger.LogError("Run step {StepId} has empty Command; pushing failure result", step.StepId);
        var failure = new StepResult(StepResult.CurrentSchemaVersion, step.StepId,
            ExitCode: -1, TimedOut: false, DurationSeconds: 0,
            ErrorMessage: "Run step missing required Command field");
        await bus.PushResultAsync(jobId, failure, cancellationToken);
    }
}
