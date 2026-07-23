using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0367: <see cref="IRunEventFanout"/> decorator that bounds every send. A group
/// SendAsync that stalls on one saturated client transport must not block the
/// broadcaster's drain loop for all other runs and tabs — the observed
/// ECONNREFUSED cascade. Each send runs under a per-send timeout; on breach the
/// send is abandoned and drop-logged, and any later fault is observed off-loop.
/// </summary>
public sealed class BackpressureSafeFanout(
    IRunEventFanout inner,
    FanoutBackpressureOptions options,
    ILogger<BackpressureSafeFanout> logger) : IRunEventFanout
{
    public Task ToOverviewAsync(RunSnapshot snapshot, CancellationToken ct) =>
        BoundedAsync(() => inner.ToOverviewAsync(snapshot, ct), "overview", snapshot.RunId, ct);

    public Task ToRunAsync(string runId, RunEvent runEvent, CancellationToken ct) =>
        BoundedAsync(() => inner.ToRunAsync(runId, runEvent, ct), "run", runId, ct);

    public Task ToSandboxAsync(string runId, string repo, RunEvent runEvent, CancellationToken ct) =>
        BoundedAsync(() => inner.ToSandboxAsync(runId, repo, runEvent, ct), "sandbox", runId, ct);

    public Task ToRunActivityAsync(string runId, SandboxActivityRollup rollup, CancellationToken ct) =>
        BoundedAsync(() => inner.ToRunActivityAsync(runId, rollup, ct), "run-activity", runId, ct);

    public Task ToSystemAsync(SystemEvent systemEvent, CancellationToken ct) =>
        BoundedAsync(() => inner.ToSystemAsync(systemEvent, ct), "system", systemEvent.Type.ToString(), ct);

    public Task ToSystemActivityAsync(SystemActivitySnapshot snapshot, CancellationToken ct) =>
        BoundedAsync(() => inner.ToSystemActivityAsync(snapshot, ct), "system-activity", "overview", ct);

    private async Task BoundedAsync(Func<Task> send, string channel, string target, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var task = send();
        var completed = await Task.WhenAny(task, Task.Delay(options.SendTimeout, timeoutCts.Token));
        if (completed == task)
        {
            timeoutCts.Cancel();
            await ObserveAsync(task, channel, target);
            return;
        }
        logger.LogWarning(
            "Fan-out to {Channel} for {Target} exceeded {TimeoutMs}ms — dropped to protect the loop",
            channel, target, options.SendTimeout.TotalMilliseconds);
        _ = ObserveAsync(task, channel, target);
    }

    private async Task ObserveAsync(Task task, string channel, string target)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Fan-out to {Channel} for {Target} faulted", channel, target);
        }
    }
}
