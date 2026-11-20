using System.Diagnostics;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-20-24bd: lets a model call through once its run holds a place (<see cref="ModelRunSlotKey"/>). The first
/// call of a run takes it; every later call of the run — the master's, a sub-agent's, a compaction's, one through
/// another agent — passes on it, so a run never waits on itself and never holds a second place. The hold is checked
/// against the store again every <see cref="ConfirmEvery"/>: a place deleted under a running run is taken again.
/// A call with no ambient run, or through an agent that sets no limit, is not gated.
/// <para>
/// While every place is held by other runs the call waits — woken by a release in this process, polling for one on
/// another replica, re-reading the configured limit each time, cancelled with its run — and says so once per wait:
/// a step line on the run and a waiting line on a design turn, naming the model, the limit and its config key. The
/// wait is recorded in the store, so neither wall-time clock counts it.
/// </para>
/// </summary>
public sealed class ModelRunGate(
    IModelRunSlots slots,
    ModelRunSlotSignal signal,
    IRunContextAccessor runContext,
    ModelRunWaitLine waitLine,
    ILogger<ModelRunGate> logger,
    IServiceProvider? services = null,
    TimeProvider? clock = null)
{
    /// <summary>How long a confirmed hold is trusted before the store is asked again.</summary>
    public static readonly TimeSpan ConfirmEvery = TimeSpan.FromSeconds(30); // not a limit: a cache's freshness

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>How long a waiting call sleeps before it asks the store again when no release in this process woke it.</summary>
    internal TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Returns once the ambient run may call <paramref name="agent"/>'s deployment.</summary>
    public async Task EnterAsync(AgentConfig agent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (agent.MaxConcurrentRuns is not { } configured) return;
        var runId = runContext.CurrentRunId;
        if (string.IsNullOrEmpty(runId)) return;
        if (AgentConfig.ConcurrencyProblem(configured) is { } problem) throw new ConfigurationException(problem);
        if (Confirmed(runId)) return;

        var entering = signal.Entering(runId);
        await entering.WaitAsync(cancellationToken);
        try
        {
            if (Confirmed(runId)) return;
            // One place per run: a hold on another key is re-confirmed there, never doubled here.
            var key = signal.HeldBy(runId)?.Key ?? ModelRunSlotKey.For(agent);
            if (await WaitForPlaceAsync(agent, key, configured, runId, cancellationToken))
                signal.Held(runId, key, _clock.GetUtcNow());
        }
        finally
        {
            entering.Release();
        }
    }

    private bool Confirmed(string runId) =>
        signal.HeldBy(runId) is { } held && _clock.GetUtcNow() - held.ConfirmedAt < ConfirmEvery;

    // True once the run holds its place; false when the limit was lifted while it waited (nothing to hold).
    private async Task<bool> WaitForPlaceAsync(
        AgentConfig agent, string key, int configured, string runId, CancellationToken cancellationToken)
    {
        Stopwatch? waited = null;
        try
        {
            while (true)
            {
                var woken = signal.NextRelease; // taken BEFORE asking, so a release in between is not missed
                if (CurrentLimit(key, configured) is not { } limit) return false;
                if (AgentConfig.ConcurrencyProblem(limit) is { } problem) throw new ConfigurationException(problem);
                if (await slots.AcquireAsync(key, limit, runId, cancellationToken)) return true;
                if (waited is null)
                {
                    waited = Stopwatch.StartNew();
                    await slots.WaitStartedAsync(runId, cancellationToken);
                    await waitLine.ShowAsync(agent, limit, runId, cancellationToken);
                }
                await Task.WhenAny(woken, Task.Delay(PollInterval, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        finally
        {
            if (waited is not null) await EndWaitAsync(runId, key, waited.ElapsedMilliseconds);
        }
    }

    // The configuration as it stands now (a studio edit reaches a waiting run); the agent's own value without one.
    private int? CurrentLimit(string key, int configured) =>
        services?.GetService<ConfiguredAgents>() is { } agents ? agents.LimitFor(key, configured) : configured;

    private async Task EndWaitAsync(string runId, string key, long waitedMs)
    {
        try
        {
            await slots.WaitEndedAsync(runId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Run {RunId}: the end of its wait for a model place could not be recorded", runId);
        }
        logger.LogInformation("Run {RunId} waited {WaitMs}ms for its place on {Key}", runId, waitedMs, key);
    }
}
