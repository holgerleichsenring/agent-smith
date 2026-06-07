using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// Runs the relational retention prune on a daily cadence (registered only when
/// persistence is configured). Bounds the RunEvent trail + non-final RunArtifact
/// growth; Run/RunRepo + the cost summary are kept. Idempotent, so safe on every
/// replica.
/// </summary>
public sealed class RunRetentionHostedService(
    IServiceProvider services,
    ILogger<RunRetentionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Cadence = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Scope per prune — RunRetentionService uses the scoped unit of work.
                using var scope = services.CreateScope();
                var retention = scope.ServiceProvider.GetRequiredService<RunRetentionService>();
                var pruned = await retention.PruneAsync(RunRetentionService.DefaultRetention, stoppingToken);
                if (pruned > 0) logger.LogInformation("Retention pruned {Count} aged rows", pruned);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Retention prune failed"); }

            try { await Task.Delay(Cadence, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
