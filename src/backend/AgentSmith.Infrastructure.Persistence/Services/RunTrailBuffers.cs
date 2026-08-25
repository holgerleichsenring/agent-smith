using System.Collections.Concurrent;
using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-08-24-ca23: the per-run trail buffers, and where each one's sequence starts. A buffer
/// created for a run that already has history — a relaunch after a pause, or a run alive across
/// a server restart — must continue the store's numbering; starting at zero re-mints sequences
/// the previous instance already wrote, which is what let replayed rows collide by value rather
/// than be recognisable as replays.
/// </summary>
public sealed class RunTrailBuffers(IServiceScopeFactory scopeFactory)
{
    private readonly ConcurrentDictionary<string, RunTrailBuffer> _buffers = new();
    // The seed is a query, and a concurrent dictionary's value factory can neither await one nor
    // promise to run once. So the buffer is resolved under this gate and published afterwards,
    // which also makes "one seed query per run" true rather than hopeful.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public IEnumerable<(string RunId, RunTrailBuffer Buffer)> All() =>
        _buffers.Select(pair => (pair.Key, pair.Value));

    public void Release(string runId) => _buffers.TryRemove(runId, out _);

    public async Task<RunTrailBuffer> ForAsync(string runId, CancellationToken ct)
    {
        if (_buffers.TryGetValue(runId, out var existing)) return existing;
        await _gate.WaitAsync(ct);
        try
        {
            if (_buffers.TryGetValue(runId, out existing)) return existing;
            var created = new RunTrailBuffer(await NextSeqAsync(runId, ct));
            _buffers[runId] = created;
            return created;
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// One past what the store holds for this run. A range scan over the run rather than a seek
    /// — the trail's index leads with a different second column — which is why it happens once
    /// per buffer and not per event. <see cref="RunTerminalReconciler"/> derives its number the
    /// same way and runs at cold start, before the drain goes live, so the store stays the
    /// single source of the sequence and the two can never mint it concurrently.
    /// </summary>
    private async Task<long> NextSeqAsync(string runId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var highest = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .Set<Entities.RunEvent>().AsNoTracking()
            .Where(e => e.RunId == runId)
            .MaxAsync(e => (long?)e.Seq, ct);
        return (highest ?? -1) + 1;
    }
}
