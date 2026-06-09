using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0140c: builds per-tracker pollers from AgentSmithConfig. Replaces the pre-p0140c
/// per-project enumeration — N projects sharing one polling-enabled tracker now spawn
/// one poller, not N. Pollers are constructed with a config snapshot; operator config
/// edits take effect on the next leader-acquisition cycle (matches today's reload model).
/// </summary>
internal static class PollerFactory
{
    public static IEnumerable<IEventPoller> Build(
        IServiceProvider provider, AgentSmithConfig config)
    {
        var ticketFactory = provider.GetRequiredService<ITicketProviderFactory>();
        var envelopeResolver = provider.GetRequiredService<IEnvelopeProjectResolver>();
        var spawnUseCase = provider.GetRequiredService<ISpawnPipelineRunsUseCase>();
        var activeRunLease = provider.GetRequiredService<IActiveRunLease>();
        var systemEvents = provider.GetRequiredService<ISystemEventPublisher>();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("AgentSmith.Server.PollerFactory");

        var enabled = config.Trackers.Values.Where(t => t.Polling.Enabled).ToList();
        logger.LogInformation(
            "PollerFactory.Build: {Count} polling-enabled tracker(s) of {Total}",
            enabled.Count, config.Trackers.Count);

        foreach (var tracker in enabled)
        {
            logger.LogInformation(
                "  built poller for tracker '{Tracker}' ({Type}) every {Interval}s",
                tracker.Name, tracker.Type, tracker.Polling.IntervalSeconds);
            yield return new TrackerPoller(
                tracker, config, ticketFactory, envelopeResolver, spawnUseCase, activeRunLease, systemEvents,
                loggerFactory.CreateLogger<TrackerPoller>());
        }
    }
}
