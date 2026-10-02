using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// 2026-10-02-5f89e: runs the RunLivenessReaper loop on every replica, beside
/// ActiveRunReaperHostedService. Its verdict only FLAGS a run; ending it is CancelEnforcer's,
/// under the housekeeping leader.
/// </summary>
public sealed class RunLivenessReaperHostedService(IServiceProvider services) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);

    // The resolve happens before the first suspending await, as in ActiveRunReaperHostedService:
    // a reaper that cannot be built is a startup finding, not a dead host.
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RunLivenessReaper reaper;
        try
        {
            reaper = services.GetRequiredService<RunLivenessReaper>();
        }
        catch (Exception ex)
        {
            services.GetService<IStartupFindings>()?.Record(new StartupFinding(
                StartupSubsystems.Database, StartupFindingSeverity.Blocking,
                "The run-liveness reaper could not be composed, so a run whose server went away "
                + $"stays running until its wall-time budget ends it. Cause: {ex.Message}"));
            return Task.CompletedTask;
        }
        return reaper.RunAsync(ScanInterval, stoppingToken);
    }
}
