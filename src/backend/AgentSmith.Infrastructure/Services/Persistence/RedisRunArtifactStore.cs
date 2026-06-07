using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Persistence;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Persistence;

/// <summary>
/// Redis-backed in-flight storage for run artifacts. Keys are TTL-bound so an
/// abandoned run can't surface stale data on a future fresh run; the explicit
/// <see cref="ClearAsync"/> drops all three keys at pipeline end as the durability
/// boundary. TTL is set on every write so late-arriving phases extend the window.
/// </summary>
public sealed class RedisRunArtifactStore : IRunArtifactStore
{
    private const string KeyNamespace = "pipeline";
    private const string PlanSlot = "plan";
    private const string DiffSlot = "diff";
    private const string BootstrapSlot = "bootstrap";
    private const string ResultSlot = "result";
    private const string PlanMarkdownSlot = "plan-md";
    private const string AnalyzeMarkdownSlot = "analyze-md";

    private static readonly TimeSpan ResultTtl = TimeSpan.FromHours(24);

    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _ttl;
    private readonly ILogger<RedisRunArtifactStore> _logger;

    public RedisRunArtifactStore(
        IConnectionMultiplexer redis,
        AgentSmithConfig config,
        ILogger<RedisRunArtifactStore> logger)
    {
        _redis = redis;
        _ttl = TimeSpan.FromHours(Math.Max(1, config.PipelineStorage.RedisTtlHours));
        _logger = logger;
    }

    public Task WritePlanAsync(string runId, string planJson, CancellationToken ct)
        => WriteAsync(runId, PlanSlot, planJson);

    public async Task<string?> ReadPlanAsync(string runId, CancellationToken ct)
        => await ReadAsync(runId, PlanSlot);

    public Task WriteDiffAsync(string runId, string diffJson, CancellationToken ct)
        => WriteAsync(runId, DiffSlot, diffJson);

    public async Task<string?> ReadDiffAsync(string runId, CancellationToken ct)
        => await ReadAsync(runId, DiffSlot);

    public Task WriteBootstrapAsync(string runId, string bootstrapMarkdown, CancellationToken ct)
        => WriteAsync(runId, BootstrapSlot, bootstrapMarkdown);

    public async Task<string?> ReadBootstrapAsync(string runId, CancellationToken ct)
        => await ReadAsync(runId, BootstrapSlot);

    public Task WriteResultMarkdownAsync(string runId, string resultMd, CancellationToken ct)
        => WriteAsync(runId, ResultSlot, resultMd, ResultTtl);

    public async Task<string?> ReadResultMarkdownAsync(string runId, CancellationToken ct)
        => await ReadAsync(runId, ResultSlot);

    public Task WritePlanMarkdownAsync(string runId, string planMd, CancellationToken ct)
        => WriteAsync(runId, PlanMarkdownSlot, planMd, ResultTtl);

    public async Task<string?> ReadPlanMarkdownAsync(string runId, CancellationToken ct)
        => await ReadAsync(runId, PlanMarkdownSlot);

    public Task WriteAnalyzeMarkdownAsync(string runId, string analyzeMd, CancellationToken ct)
        => WriteAsync(runId, AnalyzeMarkdownSlot, analyzeMd, ResultTtl);

    public async Task<string?> ReadAnalyzeMarkdownAsync(string runId, CancellationToken ct)
        => await ReadAsync(runId, AnalyzeMarkdownSlot);

    public async Task<RunArtifactSnapshot> PromoteAsync(string runId, CancellationToken ct)
    {
        var plan = await ReadAsync(runId, PlanSlot);
        var diff = await ReadAsync(runId, DiffSlot);
        var bootstrap = await ReadAsync(runId, BootstrapSlot);
        return new RunArtifactSnapshot(plan, diff, bootstrap);
    }

    public async Task ClearAsync(string runId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(new RedisKey[]
        {
            BuildKey(runId, PlanSlot), BuildKey(runId, DiffSlot), BuildKey(runId, BootstrapSlot)
        });
        _logger.LogDebug("Cleared run-artifact keys for {RunId}", runId);
    }

    private async Task WriteAsync(string runId, string slot, string value, TimeSpan? ttlOverride = null)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(BuildKey(runId, slot), value, ttlOverride ?? _ttl);
    }

    private async Task<string?> ReadAsync(string runId, string slot)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(BuildKey(runId, slot));
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    private static string BuildKey(string runId, string slot)
        => $"{KeyNamespace}:{runId}:{slot}";
}
