using System.Collections.Concurrent;
using AgentSmith.Contracts.Persistence;

namespace AgentSmith.Application.Services.Persistence;

/// <summary>
/// In-process backing for <see cref="IRunArtifactStore"/>. Used in dev mode (CLI
/// without Redis) and in tests. TTL approximated with a per-entry timestamp;
/// expired entries are evicted lazily on the next read or promote so the store
/// behaves identically to its Redis counterpart for unit-test purposes.
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

    public Task WritePlanAsync(string runId, string planJson, CancellationToken ct)
        => WriteSlotAsync(runId, e => e with { PlanJson = planJson });

    public Task<string?> ReadPlanAsync(string runId, CancellationToken ct)
        => Task.FromResult(GetFresh(runId)?.PlanJson);

    public Task WriteDiffAsync(string runId, string diffJson, CancellationToken ct)
        => WriteSlotAsync(runId, e => e with { DiffJson = diffJson });

    public Task<string?> ReadDiffAsync(string runId, CancellationToken ct)
        => Task.FromResult(GetFresh(runId)?.DiffJson);

    public Task WriteBootstrapAsync(string runId, string bootstrapMarkdown, CancellationToken ct)
        => WriteSlotAsync(runId, e => e with { BootstrapMarkdown = bootstrapMarkdown });

    public Task<string?> ReadBootstrapAsync(string runId, CancellationToken ct)
        => Task.FromResult(GetFresh(runId)?.BootstrapMarkdown);

    public Task WriteResultMarkdownAsync(string runId, string resultMd, CancellationToken ct)
        => WriteSlotAsync(runId, e => e with { ResultMd = resultMd });

    public Task<string?> ReadResultMarkdownAsync(string runId, CancellationToken ct)
        => Task.FromResult(GetFresh(runId)?.ResultMd);

    public Task WritePlanMarkdownAsync(string runId, string planMd, CancellationToken ct)
        => WriteSlotAsync(runId, e => e with { PlanMd = planMd });

    public Task<string?> ReadPlanMarkdownAsync(string runId, CancellationToken ct)
        => Task.FromResult(GetFresh(runId)?.PlanMd);

    public Task WriteAnalyzeMarkdownAsync(string runId, string analyzeMd, CancellationToken ct)
        => WriteSlotAsync(runId, e => e with { AnalyzeMd = analyzeMd });

    public Task<string?> ReadAnalyzeMarkdownAsync(string runId, CancellationToken ct)
        => Task.FromResult(GetFresh(runId)?.AnalyzeMd);

    public Task<RunArtifactSnapshot> PromoteAsync(string runId, CancellationToken ct)
    {
        var entry = GetFresh(runId);
        if (entry is null) return Task.FromResult(RunArtifactSnapshot.Empty);
        return Task.FromResult(new RunArtifactSnapshot(entry.PlanJson, entry.DiffJson, entry.BootstrapMarkdown));
    }

    public Task ClearAsync(string runId, CancellationToken ct)
    {
        _entries.TryRemove(runId, out _);
        return Task.CompletedTask;
    }

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
        string? PlanJson = null,
        string? DiffJson = null,
        string? BootstrapMarkdown = null,
        string? ResultMd = null,
        string? PlanMd = null,
        string? AnalyzeMd = null,
        DateTimeOffset StoredAt = default);
}
