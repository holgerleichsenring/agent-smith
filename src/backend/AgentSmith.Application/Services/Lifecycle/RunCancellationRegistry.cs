using System.Collections.Concurrent;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Singleton registry of per-run CancellationTokenSources keyed by runId.
/// ExecutePipelineUseCase registers at run start, the cancel endpoint or
/// the watchdog calls TryCancel by runId; the in-flight token observes
/// cancellation and the executor returns.
/// </summary>
public sealed class RunCancellationRegistry(
    ILogger<RunCancellationRegistry> logger) : IRunCancellationRegistry
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public CancellationToken Register(string runId, CancellationToken parent)
    {
        var entry = new Entry(CancellationTokenSource.CreateLinkedTokenSource(parent), DateTimeOffset.UtcNow);
        if (_entries.TryAdd(runId, entry)) return entry.Source.Token;
        entry.Source.Dispose();
        logger.LogWarning("RunCancellationRegistry: runId {RunId} already registered; reusing existing token", runId);
        return _entries[runId].Source.Token;
    }

    public bool TryCancel(string runId)
    {
        if (!_entries.TryGetValue(runId, out var entry)) return false;
        if (entry.Source.IsCancellationRequested) return true;
        try { entry.Source.Cancel(); }
        catch (ObjectDisposedException) { return false; }
        logger.LogInformation("RunCancellationRegistry: signalled cancel for runId {RunId}", runId);
        return true;
    }

    public void Unregister(string runId)
    {
        if (!_entries.TryRemove(runId, out var entry)) return;
        entry.Source.Dispose();
    }

    public IReadOnlyCollection<RunCancellationEntry> Snapshot() =>
        _entries.Select(kv => new RunCancellationEntry(kv.Key, kv.Value.RegisteredAt)).ToArray();

    private sealed record Entry(CancellationTokenSource Source, DateTimeOffset RegisteredAt);
}
