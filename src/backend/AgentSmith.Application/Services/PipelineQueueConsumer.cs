using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Pulls PipelineRequests off IRedisJobQueue and runs them with bounded concurrency
/// (SemaphoreSlim = backpressure knob). On shutdown, stops pulling and waits up to
/// shutdownGraceSeconds for in-flight pipelines to finish (so they can transition to Failed
/// and release their heartbeat once p95c lands).
/// </summary>
public sealed class PipelineQueueConsumer(
    IServiceProvider services,
    IRedisJobQueue queue,
    string configPath,
    int maxParallelJobs,
    int shutdownGraceSeconds,
    ILogger<PipelineQueueConsumer> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(maxParallelJobs);
        var inFlight = new List<Task>();
        logger.LogInformation(
            "PipelineQueueConsumer started (max parallel: {Max}, grace: {Grace}s)",
            maxParallelJobs, shutdownGraceSeconds);

        try
        {
            await foreach (var request in queue.ConsumeAsync(cancellationToken))
            {
                await semaphore.WaitAsync(cancellationToken);
                logger.LogInformation(
                    "Dequeued: {Project}/#{Ticket} pipeline={Pipeline} (in-flight: {InFlight}/{Max})",
                    request.ProjectName, request.TicketId?.Value ?? "—",
                    request.PipelineName, inFlight.Count(t => !t.IsCompleted) + 1, maxParallelJobs);
                // p0137c: removed Task.Run wrapper — RunOneAsync is async and returns
                // its task directly; the threadpool offload was defensive against a
                // hypothetical synchronous prefix that doesn't exist.
                var task = RunOneAsync(request, semaphore, cancellationToken);
                inFlight.Add(task);
                inFlight.RemoveAll(t => t.IsCompleted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("PipelineQueueConsumer loop cancelled — graceful shutdown");
        }
        catch (OperationCanceledException ex)
        {
            // Not our shutdown token — an unexpected cancellation propagated up the
            // consume loop. Don't swallow it silently; it would otherwise look like
            // a clean shutdown and hide why the consumer stopped.
            logger.LogWarning(ex, "PipelineQueueConsumer loop cancelled WITHOUT a shutdown signal");
        }

        await AwaitGraceAsync(inFlight);
        logger.LogInformation("PipelineQueueConsumer stopped");
    }

    private async Task RunOneAsync(
        PipelineRequest request, SemaphoreSlim semaphore, CancellationToken ct)
    {
        try
        {
            // AsyncServiceScope: PipelineSandboxCoordinator is IAsyncDisposable,
            // so the scope must dispose asynchronously — `using var` falls back
            // to Dispose() and throws on async-only disposables.
            await using var scope = services.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<ExecutePipelineUseCase>();
            var result = await useCase.ExecuteAsync(request, configPath, ct);
            logger.Log(
                result.IsSuccess ? LogLevel.Information : LogLevel.Warning,
                "Pipeline {Outcome}: {Project}/{Ticket} — {Msg}",
                result.IsSuccess ? "succeeded" : "failed",
                request.ProjectName, request.TicketId?.Value, result.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Pipeline execution error for {Project}/{Ticket}",
                request.ProjectName, request.TicketId?.Value);
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
