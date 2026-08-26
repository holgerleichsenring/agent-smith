using AgentSmith.Contracts.Services;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// 2026-08-26-7a51: drops observed callers past the configured retention window. The
/// window is read from the mapping in force on every pass rather than captured at startup,
/// so shortening it applies without a restart like every other value on that document.
/// </summary>
internal sealed class ObservedCallerRetentionHostedService(
    IObservedCallerStore store,
    RoleMappingSource mapping,
    TimeProvider clock,
    ILogger<ObservedCallerRetentionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepAsync(stoppingToken);
            try { await Task.Delay(SweepInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        var days = mapping.Current().Mapping.ObservationRetentionDays;
        if (days <= 0) return;
        try
        {
            var removed = await store.RemoveSeenBeforeAsync(
                clock.GetUtcNow().AddDays(-days), cancellationToken);
            if (removed > 0)
                logger.LogInformation("Dropped {Count} observed caller(s) past {Days} day(s)", removed, days);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The observed-caller retention sweep failed — retrying next pass");
        }
    }
}
