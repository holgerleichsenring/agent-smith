using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0113: Pulls PipelineRequests off IRedisJobQueue and dispatches them to
/// ephemeral CLI containers via IPipelineJobDispatcher. The Server pod is
/// orchestration-only — language toolchains live in the per-run job container.
///
/// SemaphoreSlim bounds concurrent dispatch calls (fast — SETEX + spawn API).
/// In-flight pipeline count downstream is governed by the cluster (K8s/Docker
/// capacity), not this knob. On shutdown, waits up to shutdownGraceSeconds
/// for in-flight dispatch calls to settle.
/// </summary>
public sealed class PipelineQueueConsumer(
    IServiceProvider services,
    IRedisJobQueue queue,
    string configPath,
    int maxParallelJobs,
    int shutdownGraceSeconds,
    ILogger<PipelineQueueConsumer> logger)
{
    // configPath is unused post-p0113 (dispatch carries the path through the
    // Server's ServerContext). Retained on the constructor so the hosted-service
    // wiring stays stable; can be dropped in a follow-up.
    private readonly string _configPath = configPath;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(maxParallelJobs);
        var inFlight = new List<Task>();
        logger.LogInformation(
            "PipelineQueueConsumer started (max parallel dispatches: {Max}, grace: {Grace}s)",
            maxParallelJobs, shutdownGraceSeconds);

        try
        {
            await foreach (var request in queue.ConsumeAsync(cancellationToken))
            {
                await semaphore.WaitAsync(cancellationToken);
                logger.LogInformation(
                    "Dequeued: {Project}/#{Ticket} pipeline={Pipeline} (in-flight dispatch: {InFlight}/{Max})",
                    request.ProjectName, request.TicketId?.Value ?? "—",
                    request.PipelineName, inFlight.Count(t => !t.IsCompleted) + 1, maxParallelJobs);
                var task = Task.Run(
                    () => RunOneAsync(request, semaphore, cancellationToken),
                    cancellationToken);
                inFlight.Add(task);
                inFlight.RemoveAll(t => t.IsCompleted);
            }
        }
        catch (OperationCanceledException) { /* graceful shutdown */ }

        await AwaitGraceAsync(inFlight);
        logger.LogInformation("PipelineQueueConsumer stopped");
    }

    private async Task RunOneAsync(
        PipelineRequest request, SemaphoreSlim semaphore, CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IPipelineJobDispatcher>();
            var jobId = await dispatcher.DispatchAsync(request, ct);
            logger.LogInformation(
                "Dispatched: {Project}/{Ticket} job={JobId}",
                request.ProjectName, request.TicketId?.Value ?? "—", jobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Dispatch failed for {Project}/{Ticket} — request remains claimed; reconciler will recover",
                request.ProjectName, request.TicketId?.Value ?? "—");
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task AwaitGraceAsync(List<Task> inFlight)
    {
        if (inFlight.Count == 0) return;
        var done = Task.WhenAll(inFlight);
        var deadline = Task.Delay(TimeSpan.FromSeconds(shutdownGraceSeconds));
        var winner = await Task.WhenAny(done, deadline);
        if (winner != done)
        {
            var pending = inFlight.Count(t => !t.IsCompleted);
            logger.LogWarning(
                "{Count} in-flight pipelines did not complete within {Grace}s grace period",
                pending, shutdownGraceSeconds);
        }
    }
}
