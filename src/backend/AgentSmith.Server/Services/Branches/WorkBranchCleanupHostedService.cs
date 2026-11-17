using AgentSmith.Application.Services.Health;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Branches;

/// <summary>
/// 2026-11-17-713ec: the 'branch-cleanup' subsystem — under its own leader lease, rebuilt by
/// <see cref="LeaderEpochLoop"/> on every config epoch, it starts one branch-cleanup run per configured project and
/// then waits branch_cleanup.interval_seconds. It starts runs and writes nothing to a repository itself: the
/// deletes are the run's step.
/// </summary>
public sealed class WorkBranchCleanupHostedService(
    IServiceProvider services,
    ServerContext serverContext,
    IConfigurationLoader configLoader,
    ILogger<WorkBranchCleanupHostedService> logger) : BackgroundService
{
    public const string Subsystem = "branch-cleanup";
    private const string LeaseKey = "agentsmith:leader:branch-cleanup";
    private readonly SubsystemHealth _health = new(Subsystem);

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(services, _health, LeaseKey, ct =>
            services.GetRequiredService<LeaderEpochLoop>().RunAsync(Subsystem, PassesAsync, ct), retry, stoppingToken);
    }

    /// <summary>The passes of one config epoch: none when disabled; otherwise the first at once, the rest after the
    /// interval.</summary>
    public async Task PassesAsync(AgentSmithConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.BranchCleanup.Enabled)
        {
            logger.LogInformation("Branch cleanup is off (branch_cleanup.enabled)");
            await Task.Delay(Timeout.Infinite, ct);
            return;
        }
        var interval = TimeSpan.FromSeconds(config.BranchCleanup.IntervalSeconds);
        while (!ct.IsCancellationRequested)
        {
            try { await LaunchAllAsync(config, ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogWarning(ex, "Branch cleanup pass failed; retried next interval"); }
            await Task.Delay(interval, ct);
        }
    }

    private async Task LaunchAllAsync(AgentSmithConfig config, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var launcher = scope.ServiceProvider.GetRequiredService<WorkBranchCleanupLauncher>();
        foreach (var project in config.Projects.Values)
            await launcher.LaunchAsync(project, ct);
    }
}
