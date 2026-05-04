using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Queue;

/// <summary>
/// Redis-backed handoff for queue-spawned jobs (p0113). One key per jobId,
/// SETEX with TTL — survives short dispatcher↔container races without
/// growing unbounded. RedisJobQueue already uses the same JsonSerializer
/// shape for PipelineRequest, so this reuses that round-trip contract.
/// </summary>
public sealed class RedisPipelineRequestStore(
    IConnectionMultiplexer redis,
    ILogger<RedisPipelineRequestStore> logger) : IPipelineRequestStore
{
    private const string KeyPrefix = "agentsmith:pipeline-request:";

    public async Task SaveAsync(
        string jobId, PipelineRequest request, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var json = JsonSerializer.Serialize(request);
        await db.StringSetAsync($"{KeyPrefix}{jobId}", json, ttl);
        logger.LogDebug(
            "Saved PipelineRequest for job {JobId} (project {Project}, pipeline {Pipeline}, ttl {Ttl}s)",
            jobId, request.ProjectName, request.PipelineName, (int)ttl.TotalSeconds);
    }

    public async Task<PipelineRequest?> LoadAsync(string jobId, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var json = await db.StringGetAsync($"{KeyPrefix}{jobId}");
        if (json.IsNullOrEmpty)
        {
            logger.LogWarning("PipelineRequest not found for job {JobId} — key missing or expired", jobId);
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<PipelineRequest>(json!);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize PipelineRequest for job {JobId}", jobId);
            return null;
        }
    }
}
