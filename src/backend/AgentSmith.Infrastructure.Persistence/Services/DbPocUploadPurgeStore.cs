using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-c81ee: IPocUploadPurgeStore, a scope per operation over the scoped repository.</summary>
public sealed class DbPocUploadPurgeStore(IServiceScopeFactory scopeFactory) : IPocUploadPurgeStore
{
    public Task<int> PurgeUploadsAsync(DateTimeOffset olderThan, CancellationToken ct) => With(r => r.PurgeUploadsAsync(olderThan, ct));

    public Task<IReadOnlyList<long>> DeletedWithVariablesAsync(CancellationToken ct) => With(r => r.DeletedWithVariablesAsync(ct));

    public Task ClearPlainVariablesAsync(long pocId, CancellationToken ct) =>
        With(async r => { await r.ClearPlainVariablesAsync(pocId, ct); return true; });

    private async Task<T> With<T>(Func<PocUploadPurgeRepository, Task<T>> work)
    {
        using var scope = scopeFactory.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<PocUploadPurgeRepository>());
    }
}
