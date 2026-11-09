using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-09-9631a: runs one step beside the agent's heartbeat, so a sandbox removed under a
/// read is noticed in seconds instead of at the step timeout. Removing a sandbox to free capacity
/// is right; what must not happen is a turn standing still because of it.
/// <para>
/// A long step is not a lost sandbox: the heartbeat comes from a timer independent of the step.
/// Two misses alone do not decide it either, because the probe also answers no when it could not
/// read Redis and acting on that would delete a live sandbox. The sandbox's own exit read has the
/// last word. Only a holdable sandbox is watched: the in-process one has no heartbeat to read.
/// </para>
/// </summary>
public sealed class SandboxStepWatch(
    ISandboxHeartbeatProbe heartbeat, ILogger<SandboxStepWatch> logger, TimeSpan? interval = null)
{
    private const int MissesBeforeAsking = 2;

    private readonly TimeSpan _interval = interval ?? TimeSpan.FromSeconds(3);

    /// <exception cref="SandboxVanishedException">The sandbox is gone; the step has been abandoned.</exception>
    public async Task<StepResult> RunAsync(
        ISandbox sandbox, Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        if (sandbox is not IHoldableSandbox) return await sandbox.RunStepAsync(step, progress, cancellationToken);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var running = sandbox.RunStepAsync(step, progress, stop.Token);
        var watching = WatchAsync(sandbox, stop.Token);
        var first = await Task.WhenAny(running, watching);
        await stop.CancelAsync();
        if (first == running) return await running;
        cancellationToken.ThrowIfCancellationRequested();
        await AbandonAsync(running);
        throw new SandboxVanishedException(sandbox.JobId);
    }

    private async Task WatchAsync(ISandbox sandbox, CancellationToken stop)
    {
        var misses = 0;
        while (true)
        {
            misses = await IsAliveAsync(sandbox, stop) ? 0 : misses + 1;
            if (misses >= MissesBeforeAsking)
            {
                if (await IsGoneAsync(sandbox, stop)) return;
                misses = 0;
            }
            await Task.Delay(_interval, stop);
        }
    }

    // A probe that threw says nothing about the sandbox; the channel's own timeout stays the backstop.
    private async Task<bool> IsAliveAsync(ISandbox sandbox, CancellationToken stop)
    {
        try
        {
            return await heartbeat.IsAliveAsync(sandbox.JobId, stop);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Heartbeat probe failed for sandbox {JobId}", sandbox.JobId);
            return true;
        }
    }

    private static async Task<bool> IsGoneAsync(ISandbox sandbox, CancellationToken stop) =>
        sandbox is not ISandboxExitReader reader
        || (await reader.ReadExitAsync(stop)).State != SandboxExitState.Running;

    private static async Task AbandonAsync(Task running)
    {
        try
        {
            await running;
        }
        catch (Exception)
        {
            // The step went with its sandbox: whatever it ended in, nobody is waiting for it.
        }
    }
}
