using AgentSmith.Contracts.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0246e: the run's markdown slots (result.md / plan.md / spec.md / analyze.md) live in
/// the database, so they survive a process restart and a Redis flush. 2026-10-02-5ab2f:
/// no longer a decorator over a Redis copy — the database is the only store on the server.
/// </summary>
public sealed class DbRunArtifactStore(IServiceScopeFactory scopeFactory) : IRunArtifactStore
{
    private const string ResultMd = "result_md", PlanMd = "plan_md", AnalyzeMd = "analyze_md";
    private const string SpecMd = "spec_md"; // p0390

    public Task WriteResultMarkdownAsync(string runId, string resultMd, CancellationToken ct)
        => UpsertAsync(runId, ResultMd, resultMd, ct);
    public Task<string?> ReadResultMarkdownAsync(string runId, CancellationToken ct)
        => ReadAsync(runId, ResultMd, ct);

    public Task WritePlanMarkdownAsync(string runId, string planMd, CancellationToken ct)
        => UpsertAsync(runId, PlanMd, planMd, ct);
    public Task<string?> ReadPlanMarkdownAsync(string runId, CancellationToken ct)
        => ReadAsync(runId, PlanMd, ct);

    public Task WriteSpecMarkdownAsync(string runId, string specMd, CancellationToken ct)
        => UpsertAsync(runId, SpecMd, specMd, ct);
    public Task<string?> ReadSpecMarkdownAsync(string runId, CancellationToken ct)
        => ReadAsync(runId, SpecMd, ct);

    public Task WriteAnalyzeMarkdownAsync(string runId, string analyzeMd, CancellationToken ct)
        => UpsertAsync(runId, AnalyzeMd, analyzeMd, ct);
    public Task<string?> ReadAnalyzeMarkdownAsync(string runId, CancellationToken ct)
        => ReadAsync(runId, AnalyzeMd, ct);

    // The store is a singleton (resolved by singleton markdown readers) but the DB
    // write/read is a unit of work — open a scope per operation.
    private async Task UpsertAsync(string runId, string kind, string content, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RunArtifactRepository>().UpsertAsync(runId, kind, content, ct);
    }

    private async Task<string?> ReadAsync(string runId, string kind, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RunArtifactRepository>().ReadAsync(runId, kind, ct);
    }
}
