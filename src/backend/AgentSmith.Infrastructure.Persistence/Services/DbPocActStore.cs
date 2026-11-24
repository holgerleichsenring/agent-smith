using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-c81ed: IPocActStore, a scope per operation over the scoped repository — the DbPocGitOpsStore
/// pattern.</summary>
public sealed class DbPocActStore(IServiceScopeFactory scopeFactory) : IPocActStore
{
    public Task<PocActSubject?> ReadAsync(long pocId, CancellationToken ct) => With(r => r.ReadAsync(pocId, ct));

    public Task<PocActRevision?> RevisionAsync(long pocId, int revision, CancellationToken ct) =>
        With(r => r.RevisionAsync(pocId, revision, ct));

    private async Task<T> With<T>(Func<PocActRepository, Task<T>> work)
    {
        using var scope = scopeFactory.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<PocActRepository>());
    }
}
