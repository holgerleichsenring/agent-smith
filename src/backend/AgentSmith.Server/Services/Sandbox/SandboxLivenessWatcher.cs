using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0201: per-sandbox liveness probe. Polls the agent's heartbeat key every
/// <see cref="PollInterval"/>; on <see cref="MissThreshold"/> consecutive
/// missing reads it docker-inspects the container. If the container reports
/// Running the watcher treats the miss as a Redis hiccup or in-agent GC
/// stall and waits another tick. If the container is absent / Exited / Dead
/// it publishes <see cref="SandboxVanishedEvent"/> and signals the per-run
/// cancellation registry with reason "sandbox-vanished". One-shot: after a
/// confirmed vanish the watcher stops; nothing to keep probing.
/// </summary>
public sealed class SandboxLivenessWatcher : IAsyncDisposable
{
    public const string CancelReason = "sandbox-vanished";
    public const int MissThreshold = 3;
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IDatabase _database;
    private readonly IDockerClient _docker;
    private readonly IRunCancellationRegistry _registry;
    private readonly IEventPublisher _publisher;
    private readonly SandboxLivenessTarget _target;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private int _consecutiveMisses;
    private DateTimeOffset? _lastHeartbeatAt;
    private long _disposed;

    public SandboxLivenessWatcher(
        IConnectionMultiplexer multiplexer,
        IDockerClient docker,
        IRunCancellationRegistry registry,
        IEventPublisher publisher,
        SandboxLivenessTarget target,
        ILogger<SandboxLivenessWatcher> logger)
    {
        _database = multiplexer.GetDatabase();
        _docker = docker;
        _registry = registry;
        _publisher = publisher;
        _target = target;
        _logger = logger;
    }

    public void Start()
    {
        _loop = Task.Run(() => LoopAsync(_stop.Token));
        _logger.LogInformation(
            "SandboxLivenessWatcher started for run {RunId} sandbox {SandboxKey} (container {Container})",
            _target.RunId, _target.SandboxKey, ShortId(_target.ContainerId));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var heartbeatKey = RedisKeys.HeartbeatKey(_target.JobId);
        while (!ct.IsCancellationRequested)
        {
            try { await TickAsync(heartbeatKey, ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogDebug(ex, "SandboxLivenessWatcher tick failed"); }
            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(string heartbeatKey, CancellationToken ct)
    {
        var present = await _database.KeyExistsAsync(heartbeatKey);
        if (present)
        {
            _consecutiveMisses = 0;
            _lastHeartbeatAt = DateTimeOffset.UtcNow;
            return;
        }
        _consecutiveMisses++;
        if (_consecutiveMisses < MissThreshold) return;
        await ProbeAndMaybeCancelAsync(ct);
    }

    private async Task ProbeAndMaybeCancelAsync(CancellationToken ct)
    {
        var verdict = await ProbeContainerAsync(ct);
        if (verdict.IsRunning)
        {
            // Heartbeat-late but container alive — treat as a transient miss
            // (Redis hiccup or agent-side GC). Reset to MissThreshold-1 so a
            // single immediate recovery clears the warning state without
            // forcing another full window of misses.
            _logger.LogDebug(
                "SandboxLivenessWatcher: heartbeat missing for run {RunId} but container {Container} is Running — treating as hiccup",
                _target.RunId, ShortId(_target.ContainerId));
            _consecutiveMisses = MissThreshold - 1;
            return;
        }
        await SignalVanishedAsync(verdict.State, ct);
        _stop.Cancel();
    }

    private async Task SignalVanishedAsync(string containerState, CancellationToken ct)
    {
        _logger.LogWarning(
            "SandboxLivenessWatcher: sandbox vanished for run {RunId} container {Container} state={State}",
            _target.RunId, ShortId(_target.ContainerId), containerState);
        await PublishVanishedAsync(containerState, ct);
        _registry.TryCancel(_target.RunId, CancelReason);
    }

    private Task PublishVanishedAsync(string containerState, CancellationToken ct) =>
        _publisher.PublishAsync(new SandboxVanishedEvent(
            RunId: _target.RunId,
            JobId: _target.JobId,
            Repo: _target.SandboxKey,
            LastHeartbeatAt: _lastHeartbeatAt,
            Reason: CancelReason,
            ContainerState: containerState,
            Timestamp: DateTimeOffset.UtcNow), ct);

    private async Task<ContainerProbeVerdict> ProbeContainerAsync(CancellationToken ct)
    {
        try
        {
            var info = await _docker.Containers.InspectContainerAsync(_target.ContainerId, ct);
            var state = info.State;
            if (state?.Running == true) return new ContainerProbeVerdict(true, "Running");
            if (state is null) return new ContainerProbeVerdict(false, "Unknown");
            if (state.Dead) return new ContainerProbeVerdict(false, "Dead");
            if (!string.IsNullOrEmpty(state.Status)) return new ContainerProbeVerdict(false, state.Status);
            return new ContainerProbeVerdict(false, $"Exited({state.ExitCode})");
        }
        catch (DockerContainerNotFoundException)
        {
            return new ContainerProbeVerdict(false, "Gone");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "docker inspect probe failed for container {Container}", _target.ContainerId);
            return new ContainerProbeVerdict(false, "ProbeError");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        try { _stop.Cancel(); } catch (ObjectDisposedException) { }
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected on shutdown */ }
        }
        _stop.Dispose();
    }

    private static string ShortId(string id) => id.Length > 12 ? id[..12] : id;

    private readonly record struct ContainerProbeVerdict(bool IsRunning, string State);
}
