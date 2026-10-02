using AgentSmith.Application.Services.Lifecycle;
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
/// 2026-10-02-5ab2b: every popped request passes <see cref="RunStartGate"/> — claimed at the pop,
/// before the semaphore wait, and checked against the persisted cancel flag after it.
/// </summary>
public sealed class PipelineQueueConsumer(
    IServiceProvider services,
    IRedisJobQueue queue,
    RunStartGate gate,
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
                var start = await gate.ClaimAsync(request, cancellationToken);
                if (start is null) continue;
                await WaitForSlotAsync(semaphore, start, cancellationToken);
                logger.LogInformation(
                    "Dequeued: {Project}/#{Ticket} pipeline={Pipeline} (in-flight: {InFlight}/{Max})",
                    request.ProjectName, request.TicketId?.Value ?? "—",
                    request.PipelineName, inFlight.Count(t => !t.IsCompleted) + 1, maxParallelJobs);
                var task = RunOneAsync(request, start, semaphore, cancellationToken);
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

    // A claimed start waiting for a slot keeps its row beaten; a shutdown during the wait ends the beat.
    private static async Task WaitForSlotAsync(SemaphoreSlim semaphore, ClaimedRunStart start, CancellationToken ct)
    {
        try { await semaphore.WaitAsync(ct); }
        catch (OperationCanceledException)
        {
            await start.DisposeAsync();
            throw;
        }
    }

    private async Task RunOneAsync(
        PipelineRequest request, ClaimedRunStart start, SemaphoreSlim semaphore, CancellationToken ct)
    {
        await using var claimed = start;
        try
        {
            // Async scope: PipelineSandboxCoordinator is IAsyncDisposable only.
            await using var scope = services.CreateAsyncScope();
            if (await gate.RefusesStartAsync(scope.ServiceProvider, request, ct)) return;
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
