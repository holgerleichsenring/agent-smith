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


    public bool TryCancel(string runId) => TryCancel(runId, reason: "operator");

    public bool TryCancel(string runId, string reason)
    {
        if (!_entries.TryGetValue(runId, out var entry)) return false;
        if (entry.Source.IsCancellationRequested) return true;
        try { entry.Source.Cancel(); }
        catch (ObjectDisposedException) { return false; }
        Interlocked.CompareExchange(ref entry.Reason, reason, comparand: null);
        logger.LogInformation(
            "RunCancellationRegistry: signalled cancel for runId {RunId} reason {Reason}",
            runId, reason);
        return true;
    }

    public bool TryGetReason(string runId, out string reason)
    {
        if (_entries.TryGetValue(runId, out var entry) && entry.Reason is not null)
        {
            reason = entry.Reason;
            return true;
        }
        reason = string.Empty;
        return false;
    }

    public void Unregister(string runId)
    {
        if (!_entries.TryRemove(runId, out var entry)) return;
        entry.Source.Dispose();
    }

    public IReadOnlyCollection<RunCancellationEntry> Snapshot() =>
        _entries.Select(kv => new RunCancellationEntry(kv.Key, kv.Value.RegisteredAt)).ToArray();

    private sealed class Entry(CancellationTokenSource source, DateTimeOffset registeredAt)
    {
        public CancellationTokenSource Source { get; } = source;
        public DateTimeOffset RegisteredAt { get; } = registeredAt;
        // p0201: mutated exactly once via Interlocked.CompareExchange so the
        // first cancel reason wins. Field-shape (not property) so it's a
        // legal target for Interlocked.
        public string? Reason;
    }
}
