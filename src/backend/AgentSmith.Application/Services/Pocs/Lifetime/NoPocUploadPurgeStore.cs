using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: without a database no PoC upload is stored, so nothing is purged.</summary>
public sealed class NoPocUploadPurgeStore : IPocUploadPurgeStore
{
    public Task<int> PurgeUploadsAsync(DateTimeOffset olderThan, CancellationToken ct) => Task.FromResult(0);

    public Task<IReadOnlyList<long>> DeletedWithVariablesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<long>>([]);

    public Task ClearPlainVariablesAsync(long pocId, CancellationToken ct) => Task.CompletedTask;
}
