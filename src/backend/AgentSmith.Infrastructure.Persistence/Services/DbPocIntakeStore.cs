using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-933db: IPocIntakeStore for the run's step, a scope per operation over the scoped repository —
/// the DbRunCheckpointStore pattern.</summary>
public sealed class DbPocIntakeStore(IServiceScopeFactory scopeFactory) : IPocIntakeStore
{
    public Task<PocIntakeSubject?> ReadAsync(long pocId, int revision, CancellationToken ct) =>
        InScope(r => r.ReadAsync(pocId, revision, ct));

    public Task<byte[]?> ArchiveAsync(string uploadSession, string uploadId, CancellationToken ct) =>
        InScope(r => r.ArchiveAsync(uploadSession, uploadId, ct));

    public Task RecordAsync(PocIntakeOutcome outcome, CancellationToken ct) =>
        InScope(async r => { await r.RecordAsync(outcome, ct); return true; });

    private async Task<T> InScope<T>(Func<PocIntakeRepository, Task<T>> op)
    {
        using var scope = scopeFactory.CreateScope();
        return await op(scope.ServiceProvider.GetRequiredService<PocIntakeRepository>());
    }
}
