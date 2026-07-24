using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0376: ticks <see cref="RunDbProjector.FlushStaleAsync"/> on a short interval so
/// a run's raw event trail surfaces in the UI within ~a second even when it emits
/// fewer than the batch threshold, or pauses mid-batch. Without it the trail stayed
/// dark until 25 events accumulated (or the run finished) and then appeared in a
/// sudden burst. Registered only when relational persistence — and thus the
/// projector — is present.
/// </summary>
public sealed class RunTrailFlusherHostedService(
    RunDbProjector projector,
    ILogger<RunTrailFlusherHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await projector.FlushStaleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Best-effort surfacing; the next tick retries. A flush fault must not
                // stop the flusher (mirrors the heartbeat pump's transient tolerance).
                logger.LogWarning(ex, "Run trail stale-flush tick failed — retrying next tick");
            }
        }
    }
}
