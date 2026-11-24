using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-c81ec: IPocRolloutStore, a scope per operation over the scoped repository — the DbPocGitOpsStore
/// pattern.</summary>
public sealed class DbPocRolloutStore(IServiceScopeFactory scopeFactory) : IPocRolloutStore
{
    public Task<DateTimeOffset?> RunStartedAtAsync(string runId, CancellationToken ct) => With(r => r.RunStartedAtAsync(runId, ct));

    public Task<bool> HasHealthyRolloutAsync(long pocId, CancellationToken ct) => With(r => r.HasHealthyRolloutAsync(pocId, ct));

    public Task RecordCommitAsync(long pocId, int revision, string commitSha, CancellationToken ct) =>
        With(async r => { await r.RecordCommitAsync(pocId, revision, commitSha, ct); return true; });

    public Task RecordOutcomeAsync(PocRolloutOutcome outcome, CancellationToken ct) =>
        With(async r => { await r.RecordOutcomeAsync(outcome, ct); return true; });

    private async Task<T> With<T>(Func<PocRolloutRepository, Task<T>> work)
    {
        using var scope = scopeFactory.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<PocRolloutRepository>());
    }
}
