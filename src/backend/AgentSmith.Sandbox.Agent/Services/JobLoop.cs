using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

internal sealed class JobLoop(
    IRedisJobBus bus, IStepExecutor executor, IStepInFlightMarker heartbeat, ILogger<JobLoop> logger,
    string? runId = null)
{
    public const int ExitOk = 0;
    public const int ExitIdleTimeout = 2;
    public static readonly TimeSpan IdlePollTimeout = TimeSpan.FromSeconds(60);

    // p0257: consecutive idle poll cycles (× IdlePollTimeout) with NO step before
    // the agent self-terminates (exitCode 2). This is a LAST-RESORT backstop for a
    // sandbox whose SERVER died — the server-side SandboxLivenessWatcher +
    // OrphanReaper are the authoritative, fast orphan-killers. The old 5-cycle
    // (5-min) budget was far too aggressive: in a multi-repo run the analyze step
    // runs SEQUENTIALLY (~3 min/repo) and the master loop touches one repo at a
    // time, so a sandbox legitimately waits its turn for >5 min and self-killed
    // (exitCode 2 → "sandbox vanished" → run failed — the recurring bug). Default
    // 30 cycles (30 min) covers a long multi-repo run; tune via
    // AGENTSMITH_SANDBOX_MAX_IDLE_CYCLES for outliers without rebuilding the image.
    public static readonly int MaxIdleCycles = ResolveMaxIdleCycles();

    private const int DefaultMaxIdleCycles = 30;

    private static int ResolveMaxIdleCycles()
    {
        var raw = Environment.GetEnvironmentVariable("AGENTSMITH_SANDBOX_MAX_IDLE_CYCLES");
        return int.TryParse(raw, out var n) && n > 0 ? n : DefaultMaxIdleCycles;
    }

    public async Task<int> RunAsync(string jobId, CancellationToken cancellationToken)
    {
        // job scope is visible from every logger sharing this factory's
        // ExternalScopeProvider — including StepExecutor + handler loggers —
        // so `[job=<short>]` prefixes every line emitted while a step is in
        // flight, matching the server-side `[run=...] [ticket=...]` convention.
        using var jobScope = logger.BeginScope("job={Job}", ShortJobId(jobId));
        var idleCycles = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var step = await bus.WaitForStepAsync(jobId, IdlePollTimeout, cancellationToken);
            if (step is null)
            {
                if (++idleCycles >= MaxIdleCycles)
                {
                    // p0360b: the idle exit is a backstop for a DEAD server, not for a
                    // busy run. A multi-repo master legitimately leaves a sandbox idle
                    // for 30+ minutes (it works one repo / one long LLM stretch at a
                    // time); self-terminating then made the whole healthy run collapse
                    // ("sandbox vanished", no PR — the recurring 1-hour death). When
                    // the run is still in the server's active set, keep waiting.
                    if (await RunStillActiveAsync(cancellationToken))
                    {
                        logger.LogInformation(
                            "Idle limit ({Cycles} cycles) reached but run {RunId} is still active — continuing to wait",
                            idleCycles, runId);
                        idleCycles = 0;
                        continue;
                    }
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
            await ProcessExecutableStepAsync(jobId, step, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return ExitOk;
    }

    // False without a run id (old launch path / probe sandboxes) or on any Redis
    // error — fail-closed to the original backstop behavior: when in doubt, exit.
    private async Task<bool> RunStillActiveAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(runId)) return false;
        try
        {
            return await bus.IsRunActiveAsync(runId!, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Run-alive probe failed for run {RunId} — treating as not active", runId);
            return false;
        }
    }

    private async Task ProcessExecutableStepAsync(
        string jobId, Step step, CancellationToken cancellationToken)
    {
        var (isValid, error) = step.Validate();
        if (!isValid)
        {
            await PushValidationFailureAsync(jobId, step, error!, cancellationToken);
            return;
        }

        heartbeat.MarkStepInFlight(true);
        try
        {
            var result = await executor.ExecuteAsync(step,
                batch => { bus.EnqueueEventsBatch(jobId, batch); return Task.CompletedTask; },
                cancellationToken);
            await bus.PushResultAsync(jobId, result, cancellationToken);
        }
        finally
        {
            heartbeat.MarkStepInFlight(false);
        }
    }

    private static string ShortJobId(string jobId) =>
        jobId.Length > 8 ? jobId[..8] : jobId;

    private async Task PushValidationFailureAsync(
        string jobId, Step step, string error, CancellationToken cancellationToken)
    {
        logger.LogError("Step {StepId} kind {Kind} failed validation: {Error}",
            step.StepId, step.Kind, error);
        var failure = new StepResult(StepResult.CurrentSchemaVersion, step.StepId,
            ExitCode: -1, TimedOut: false, DurationSeconds: 0, ErrorMessage: error);
        await bus.PushResultAsync(jobId, failure, cancellationToken);
    }
}
