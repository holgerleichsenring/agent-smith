using AgentSmith.Application.Services.Health;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-08-9e6e: polling mode's change sweep — under its own leader lease, outside the poller's
/// 20-second budget, rebuilt when the config epoch advances. Each polling tracker entry is swept
/// at its interval with a deadline of half of it; the sweep only nudges, the rework worker decides.
/// </summary>
public sealed class ChangeSweepHostedService(
    IServiceProvider services,
    ServerContext serverContext,
    IConfigurationLoader configLoader,
    ILogger<ChangeSweepHostedService> logger) : BackgroundService
{
    private const string LeaseKey = "agentsmith:leader:change-sweep";
    private readonly SubsystemHealth _health = new("change-sweep");

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(services, _health, LeaseKey, ct =>
            services.GetRequiredService<LeaderEpochLoop>().RunAsync("change-sweep", SweepAsync, ct), retry, stoppingToken);
    }

    private async Task SweepAsync(AgentSmithConfig config, CancellationToken ct)
    {
        var polling = config.Trackers.Values.Where(t => t.Polling.Enabled).ToList();
        if (polling.Count == 0)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return;
        }
        var interval = TimeSpan.FromSeconds(Math.Max(10, polling.Min(t => t.Polling.IntervalSeconds)));
        var sources = ActivatorUtilities.CreateInstance<ChangeSweepSources>(services).For(config, polling);
        var cycle = ActivatorUtilities.CreateInstance<ChangeSweepCycle>(services);
        logger.LogInformation("Change sweep over {Sources} source(s) every {Interval}", sources.Count, interval);
        while (!ct.IsCancellationRequested)
        {
            await cycle.RunAsync(sources, interval / 2, ct);
            await Task.Delay(interval, ct);
        }
    }
}
