using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-13-9a59i: releases a run's sandboxes and records how each one stood. The exit is read
/// through the backend BEFORE the dispose — a container or pod that died mid-run is still there
/// (AutoRemove off, RestartPolicy Never) — so an OOM kill or a pod deleted under the run is on the
/// record on every backend, without a second watcher. A backend with no process of its own reads
/// nothing and the release carries no evidence.
/// </summary>
public sealed class SandboxRelease(IEventPublisher eventPublisher, ILogger<SandboxRelease> logger)
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    public async Task ReleaseAllAsync(string? runId, IReadOnlyDictionary<string, ISandbox> sandboxes)
    {
        foreach (var (key, sandbox) in sandboxes)
        {
            if (sandbox is null) continue;
            var exit = string.IsNullOrEmpty(runId) ? null : await ReadExitAsync(key, sandbox);
            await sandbox.DisposeAsync();
            await PublishDisposedAsync(runId, key, exit);
        }
    }

    private async Task<SandboxExitRead?> ReadExitAsync(string key, ISandbox sandbox)
    {
        if (sandbox.Backend() is not ISandboxExitReader reader) return null;
        try
        {
            using var timeout = new CancellationTokenSource(ReadTimeout);
            return await reader.ReadExitAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            // Unreadable is Gone — the reader's own contract, kept when the read itself fails.
            logger.LogDebug(ex, "Reading the exit of sandbox {Key} failed", key);
            return SandboxExitRead.Gone;
        }
    }

    private async Task PublishDisposedAsync(string? runId, string key, SandboxExitRead? exit)
    {
        if (string.IsNullOrEmpty(runId)) return;
        try
        {
            await eventPublisher.PublishAsync(new SandboxDisposedEvent(
                runId, key, exit?.ExitCode, DateTimeOffset.UtcNow, exit?.Label, exit?.OomKilled));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to publish SandboxDisposed for {Key}", key);
        }
    }
}
