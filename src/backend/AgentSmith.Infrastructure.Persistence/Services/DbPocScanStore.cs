using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-933dc: IPocScanStore for the run's scan and commit steps, a scope per operation over the scoped
/// repository — the DbPocIntakeStore pattern.</summary>
public sealed class DbPocScanStore(IServiceScopeFactory scopeFactory) : IPocScanStore
{
    public Task<PocScanSubject?> ReadAsync(long pocId, int revision, CancellationToken ct) =>
        InScope(r => r.ReadAsync(pocId, revision, ct));

    public Task AppendOverruledAsync(long pocId, int revision, string stage, IReadOnlyList<string> ids, CancellationToken ct) =>
        InScope(async r => { await r.AppendOverruledAsync(pocId, revision, stage, ids, ct); return true; });

    public Task RecordVerdictAsync(PocScanOutcome outcome, CancellationToken ct) =>
        InScope(async r => { await r.RecordVerdictAsync(outcome, ct); return true; });

    public Task RecordCommitAsync(long pocId, int revision, string commitSha, CancellationToken ct) =>
        InScope(async r => { await r.RecordCommitAsync(pocId, revision, commitSha, ct); return true; });

    private async Task<T> InScope<T>(Func<PocScanRepository, Task<T>> op)
    {
        using var scope = scopeFactory.CreateScope();
        return await op(scope.ServiceProvider.GetRequiredService<PocScanRepository>());
    }
}
