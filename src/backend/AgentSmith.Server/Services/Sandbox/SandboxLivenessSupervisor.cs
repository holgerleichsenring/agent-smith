using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Docker.DotNet;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0201: Server-side <see cref="ISandboxLivenessSupervisor"/>. One per
/// pipeline-run (transient lifetime, matching PipelineSandboxCoordinator).
/// Spawns one <see cref="SandboxLivenessWatcher"/> per sandbox it's told
/// about, then disposes them all on DisposeAsync.
/// </summary>
public sealed class SandboxLivenessSupervisor(
    IConnectionMultiplexer multiplexer,
    IDockerClient docker,
    IRunCancellationRegistry registry,
    IEventPublisher publisher,
    ILoggerFactory loggerFactory) : ISandboxLivenessSupervisor
{
    private readonly List<SandboxLivenessWatcher> _watchers = new();
    private readonly object _gate = new();

    public void Watch(string runId, string sandboxKey, ISandbox sandbox)
    {
        if (sandbox is not ISandboxLivenessProbeTarget target) return;
        var containerId = target.LivenessProbeTargetId;
        if (string.IsNullOrEmpty(containerId)) return;
        var watcher = new SandboxLivenessWatcher(
            multiplexer, docker, registry, publisher,
            new SandboxLivenessTarget(runId, sandbox.JobId, containerId, sandboxKey),
            loggerFactory.CreateLogger<SandboxLivenessWatcher>());
        lock (_gate) _watchers.Add(watcher);
        watcher.Start();
    }

    public async ValueTask DisposeAsync()
    {
        SandboxLivenessWatcher[] snapshot;
        lock (_gate) { snapshot = _watchers.ToArray(); _watchers.Clear(); }
        foreach (var w in snapshot) await w.DisposeAsync();
    }
}
