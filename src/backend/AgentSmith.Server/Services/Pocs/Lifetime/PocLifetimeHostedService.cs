using AgentSmith.Application.Services.Health;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Hosting;
using Microsoft.Extensions.Hosting;

namespace AgentSmith.Server.Services.Pocs.Lifetime;

/// <summary>
/// 2026-11-23-c81ee: the 'poc-lifetime' subsystem — under its own leader lease, rebuilt by <see cref="LeaderEpochLoop"/> on
/// every config epoch like branch-cleanup, it runs a <see cref="PocLifetimePass"/> every pocs.lifetime.interval_seconds. It
/// is idle while pocs.enabled is off (<see cref="PocsGate"/>, asked on every pass) or no target exists. It starts runs and
/// writes nothing itself.
/// </summary>
public sealed class PocLifetimeHostedService(
    IServiceProvider services, ServerContext serverContext, IConfigurationLoader configLoader, ILogger<PocLifetimeHostedService> logger)
    : BackgroundService
{
    public const string Subsystem = "poc-lifetime";
    private const string LeaseKey = "agentsmith:leader:poc-lifetime";
    private readonly SubsystemHealth _health = new(Subsystem);

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(services, _health, LeaseKey, ct =>
            services.GetRequiredService<LeaderEpochLoop>().RunAsync(Subsystem, PassesAsync, ct), retry, stoppingToken);
    }

    /// <summary>The passes of one config epoch: the first at once, the rest after the interval; each does nothing while
    /// PoCs are off or have no target.</summary>
    public async Task PassesAsync(AgentSmithConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        var interval = TimeSpan.FromSeconds(config.Pocs.Lifetime.IntervalSeconds);
        while (!ct.IsCancellationRequested)
        {
            try { await PassAsync(config, ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogWarning(ex, "PoC lifetime pass failed; retried next interval"); }
            await Task.Delay(interval, ct);
        }
    }

    internal async Task PassAsync(AgentSmithConfig config, CancellationToken ct)
    {
        if (!services.GetRequiredService<PocsGate>().Enabled(config) || config.Pocs.Targets.Count == 0)
        {
            logger.LogDebug("PoC lifetime pass idle: pocs.enabled is off or pocs.targets is empty");
            return;
        }
        await using var scope = services.CreateAsyncScope();
        var launched = await scope.ServiceProvider.GetRequiredService<PocLifetimePass>().RunAsync(config.Pocs, ct);
        logger.LogInformation("PoC lifetime pass started {Count} run(s)", launched);
    }
}
