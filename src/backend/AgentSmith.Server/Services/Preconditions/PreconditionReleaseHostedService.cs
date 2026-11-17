using AgentSmith.Application.Services.Health;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Preconditions;

/// <summary>
/// 2026-11-16-f3fbc: the 'preconditions' subsystem — under its own leader lease, rebuilt by
/// <see cref="LeaderEpochLoop"/> whenever the config epoch advances, so a settings save that trusts an image or
/// completes a config re-checks at once; then every preconditions.recheck_interval_seconds, which notices an init
/// that landed, since nothing reports one. An epoch start is no evidence by itself: a server restart or a lease
/// handover starts one too, so every pass judges each stop by what changed after it.
/// </summary>
public sealed class PreconditionReleaseHostedService(
    IServiceProvider services,
    ServerContext serverContext,
    IConfigurationLoader configLoader,
    ILogger<PreconditionReleaseHostedService> logger) : BackgroundService
{
    public const string Subsystem = "preconditions";
    private const string LeaseKey = "agentsmith:leader:preconditions";
    private readonly SubsystemHealth _health = new(Subsystem);

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(services, _health, LeaseKey, ct =>
            services.GetRequiredService<LeaderEpochLoop>().RunAsync(Subsystem, PassesAsync, ct), retry, stoppingToken);
    }

    /// <summary>The passes of one config epoch: the first runs at once, the rest follow the interval.</summary>
    public async Task PassesAsync(AgentSmithConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        var pass = services.GetRequiredService<PreconditionReleasePass>();
        var interval = TimeSpan.FromSeconds(config.Preconditions.RecheckIntervalSeconds);
        while (!ct.IsCancellationRequested)
        {
            try { await pass.RunAsync(config, ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogWarning(ex, "Precondition pass failed; retried next interval"); }
            await Task.Delay(interval, ct);
        }
    }
}
