using System.Collections.Concurrent;
using AgentSmith.Contracts.Persistence;

namespace AgentSmith.Application.Services.Persistence;

/// <summary>
/// In-process backing for <see cref="IRunArtifactStore"/>. Used in dev mode (CLI
/// without a database) and in tests. TTL approximated with a per-entry timestamp;
/// expired entries are evicted lazily on the next read.
/// </summary>
public sealed class InMemoryRunArtifactStore : IRunArtifactStore
{
    private readonly ConcurrentDictionary<string, RunEntry> _entries = new();
    private readonly TimeSpan _ttl;
    private readonly Func<DateTimeOffset> _clock;

    public InMemoryRunArtifactStore(TimeSpan? ttl = null, Func<DateTimeOffset>? clock = null)
    {
        _ttl = ttl ?? TimeSpan.FromHours(4);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task WriteResultMarkdownAsync(string runId, string resultMd, CancellationToken ct)
        => WriteSlotAsync(runId, e => e with { ResultMd = resultMd });

    public Task<string?> ReadResultMarkdownAsync(string runId, CancellationToken ct)
        => Task.FromResult(GetFresh(runId)?.ResultMd);

    public Task WritePlanMarkdownAsync(string runId, string planMd, CancellationToken ct)
        => WriteSlotAsync(runId, e => e with { PlanMd = planMd });

    public Task<string?> ReadPlanMarkdownAsync(string runId, CancellationToken ct)
        => Task.FromResult(GetFresh(runId)?.PlanMd);

    public Task WriteSpecMarkdownAsync(string runId, string specMd, CancellationToken ct)
        => WriteSlotAsync(runId, e => e with { SpecMd = specMd });

    public Task<string?> ReadSpecMarkdownAsync(string runId, CancellationToken ct)
        => Task.FromResult(GetFresh(runId)?.SpecMd);

    public Task WriteAnalyzeMarkdownAsync(string runId, string analyzeMd, CancellationToken ct)
        => WriteSlotAsync(runId, e => e with { AnalyzeMd = analyzeMd });

    public Task<string?> ReadAnalyzeMarkdownAsync(string runId, CancellationToken ct)
        => Task.FromResult(GetFresh(runId)?.AnalyzeMd);

    private Task WriteSlotAsync(string runId, Func<RunEntry, RunEntry> mutator)
    {
        var now = _clock();
        _entries.AddOrUpdate(runId,
            _ => mutator(new RunEntry()) with { StoredAt = now },
            (_, existing) => mutator(existing) with { StoredAt = now });
        return Task.CompletedTask;
    }

    private RunEntry? GetFresh(string runId)
    {
        if (!_entries.TryGetValue(runId, out var entry)) return null;
        if (_clock() - entry.StoredAt > _ttl)
        {
            _entries.TryRemove(runId, out _);
            return null;
        }
        return entry;
    }

    private sealed record RunEntry(
        string? ResultMd = null,
        string? PlanMd = null,
        string? SpecMd = null, // p0390
        string? AnalyzeMd = null,
        DateTimeOffset StoredAt = default);
}
