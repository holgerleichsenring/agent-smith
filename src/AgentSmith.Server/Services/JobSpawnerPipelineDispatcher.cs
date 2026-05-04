using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0113: Server-side adapter from Application's IPipelineJobDispatcher to
/// IJobSpawner. Generates jobId, persists the request via IPipelineRequestStore,
/// then spawns an ephemeral CLI container that loads the request by jobId.
/// </summary>
public sealed class JobSpawnerPipelineDispatcher(
    IJobSpawner jobSpawner,
    IPipelineRequestStore requestStore,
    ServerContext serverContext,
    ILogger<JobSpawnerPipelineDispatcher> logger) : IPipelineJobDispatcher
{
    private static readonly TimeSpan RequestTtl = TimeSpan.FromHours(1);

    public async Task<string> DispatchAsync(PipelineRequest request, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N")[..12];
        var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis:6379";

        await requestStore.SaveAsync(jobId, request, RequestTtl, cancellationToken);
        await jobSpawner.SpawnQueueJobAsync(jobId, redisUrl, serverContext.ConfigPath, cancellationToken);

        logger.LogInformation(
            "Dispatched queue job {JobId}: {Project}/#{Ticket} pipeline={Pipeline}",
            jobId, request.ProjectName, request.TicketId?.Value ?? "—", request.PipelineName);

        return jobId;
    }
}
