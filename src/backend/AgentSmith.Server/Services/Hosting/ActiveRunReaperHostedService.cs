using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
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

    // p0391b: the resolve happens before the first suspending await, so a failure faults
    // StartAsync synchronously and kills the host — the one place in this service where
    // BackgroundServiceExceptionBehavior.Ignore does not apply. A reaper that cannot be
    // built is a finding; runs then stay leased longer, and the server says why.
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ActiveRunReaper reaper;
        try
        {
            reaper = services.GetRequiredService<ActiveRunReaper>();
        }
        catch (Exception ex)
        {
            services.GetService<IStartupFindings>()?.Record(new StartupFinding(
                StartupSubsystems.Database, StartupFindingSeverity.Blocking,
                "The active-run reaper could not be composed, so a run whose sandbox died keeps "
                + $"its lease until an operator releases it. Cause: {ex.Message}"));
            return Task.CompletedTask;
        }
        return reaper.RunAsync(StaleThreshold, ScanInterval, stoppingToken);
    }
}
