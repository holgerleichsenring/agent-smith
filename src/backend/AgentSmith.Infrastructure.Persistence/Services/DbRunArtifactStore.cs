using AgentSmith.Contracts.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0246e: makes the DURABLE markdown slots (result.md / plan.md / analyze.md)
/// survive a process restart AND a Redis flush by mirroring them into the DB.
/// Decorates the existing store: markdown writes go to BOTH the inner store and
/// the DB; markdown reads prefer the DB and fall back to the inner. The transient
/// in-flight slots (plan/diff/bootstrap, handed between phases) stay with the
/// inner store only — they are not run history.
/// </summary>
public sealed class DbRunArtifactStore(
    IRunArtifactStore inner,
    IServiceScopeFactory scopeFactory) : IRunArtifactStore
{
    private const string ResultMd = "result_md", PlanMd = "plan_md", AnalyzeMd = "analyze_md";

    public Task WritePlanAsync(string runId, string planJson, CancellationToken ct)
        => inner.WritePlanAsync(runId, planJson, ct);
    public Task<string?> ReadPlanAsync(string runId, CancellationToken ct) => inner.ReadPlanAsync(runId, ct);

    public Task WriteDiffAsync(string runId, string diffJson, CancellationToken ct)
        => inner.WriteDiffAsync(runId, diffJson, ct);
    public Task<string?> ReadDiffAsync(string runId, CancellationToken ct) => inner.ReadDiffAsync(runId, ct);

    public Task WriteBootstrapAsync(string runId, string bootstrapMarkdown, CancellationToken ct)
        => inner.WriteBootstrapAsync(runId, bootstrapMarkdown, ct);
    public Task<string?> ReadBootstrapAsync(string runId, CancellationToken ct) => inner.ReadBootstrapAsync(runId, ct);

    public async Task WriteResultMarkdownAsync(string runId, string resultMd, CancellationToken ct)
    {
        await inner.WriteResultMarkdownAsync(runId, resultMd, ct);
        await UpsertAsync(runId, ResultMd, resultMd, ct);
    }
    public async Task<string?> ReadResultMarkdownAsync(string runId, CancellationToken ct)
        => await ReadAsync(runId, ResultMd, ct) ?? await inner.ReadResultMarkdownAsync(runId, ct);

    public async Task WritePlanMarkdownAsync(string runId, string planMd, CancellationToken ct)
    {
        await inner.WritePlanMarkdownAsync(runId, planMd, ct);
        await UpsertAsync(runId, PlanMd, planMd, ct);
    }
    public async Task<string?> ReadPlanMarkdownAsync(string runId, CancellationToken ct)
        => await ReadAsync(runId, PlanMd, ct) ?? await inner.ReadPlanMarkdownAsync(runId, ct);

    public async Task WriteAnalyzeMarkdownAsync(string runId, string analyzeMd, CancellationToken ct)
    {
        await inner.WriteAnalyzeMarkdownAsync(runId, analyzeMd, ct);
        await UpsertAsync(runId, AnalyzeMd, analyzeMd, ct);
    }
    public async Task<string?> ReadAnalyzeMarkdownAsync(string runId, CancellationToken ct)
        => await ReadAsync(runId, AnalyzeMd, ct) ?? await inner.ReadAnalyzeMarkdownAsync(runId, ct);

    public Task<RunArtifactSnapshot> PromoteAsync(string runId, CancellationToken ct) => inner.PromoteAsync(runId, ct);
    public Task ClearAsync(string runId, CancellationToken ct) => inner.ClearAsync(runId, ct);

    // The decorator is a singleton (resolved by singleton markdown readers) but
    // the DB write/read is a unit of work — open a scope per operation.
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
