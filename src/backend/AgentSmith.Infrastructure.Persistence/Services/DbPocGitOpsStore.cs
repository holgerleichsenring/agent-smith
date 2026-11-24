using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-c81eb: IPocGitOpsStore, a scope per operation over the scoped repository — the DbPocVariableStore
/// pattern.</summary>
public sealed class DbPocGitOpsStore(IServiceScopeFactory scopeFactory) : IPocGitOpsStore
{
    public Task<PocGitOpsSubject?> ReadAsync(long pocId, CancellationToken ct) => With(r => r.ReadAsync(pocId, ct));

    public Task<PocRenderRevision?> RevisionAsync(long pocId, int revision, CancellationToken ct) =>
        With(r => r.RevisionAsync(pocId, revision, ct));

    public Task RecordDeployedAsync(long pocId, PocDesiredState desired, string? sealFingerprint, CancellationToken ct) =>
        With(async r => { await r.RecordDeployedAsync(pocId, desired, sealFingerprint, ct); return true; });

    public Task ClearSealFingerprintAsync(long pocId, CancellationToken ct) =>
        With(async r => { await r.ClearSealFingerprintAsync(pocId, ct); return true; });

    private async Task<T> With<T>(Func<PocGitOpsRepository, Task<T>> work)
    {
        using var scope = scopeFactory.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<PocGitOpsRepository>());
    }
}
