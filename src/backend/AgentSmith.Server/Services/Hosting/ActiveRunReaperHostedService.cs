using AgentSmith.Application.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// Runs the ActiveRunReaper loop. Registered only when relational persistence is
/// configured. Safe to run on every replica: the reaper releases ONLY on positive
/// evidence (orchestrator says the container is gone) and DELETE is idempotent, so
/// no leader election is required.
/// </summary>
public sealed class ActiveRunReaperHostedService(IServiceProvider services) : BackgroundService
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reaper = services.GetRequiredService<ActiveRunReaper>();
        return reaper.RunAsync(StaleThreshold, ScanInterval, stoppingToken);
    }
}
