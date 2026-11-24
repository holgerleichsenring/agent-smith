using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-933dd: IPocBuildStore for the run's build step, a scope per operation over the scoped repository —
/// the DbPocScanStore pattern.</summary>
public sealed class DbPocBuildStore(IServiceScopeFactory scopeFactory) : IPocBuildStore
{
    public Task RecordPipelineRunAsync(long pocId, int revision, string? runId, string? url, CancellationToken ct) =>
        InScope(r => r.RecordPipelineRunAsync(pocId, revision, runId, url, ct));

    public Task RecordImageBlockAsync(long pocId, int revision, string verdictJson, CancellationToken ct) =>
        InScope(r => r.RecordImageBlockAsync(pocId, revision, verdictJson, ct));

    public Task RecordBuiltAsync(long pocId, int revision, string digest, string verdictJson, CancellationToken ct) =>
        InScope(r => r.RecordBuiltAsync(pocId, revision, digest, verdictJson, ct));

    private async Task InScope(Func<PocBuildRepository, Task> op)
    {
        using var scope = scopeFactory.CreateScope();
        await op(scope.ServiceProvider.GetRequiredService<PocBuildRepository>());
    }
}
