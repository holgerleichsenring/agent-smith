using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// p0391a: logs what this server started with — the configuration summary and the
/// deprecation warnings. Pure reporting over an already-materialized config; it runs after
/// the probes so the log reads "here is what is broken, here is what is running".
/// </summary>
public sealed class StartupAnnouncer(
    AgentSmithConfig config,
    PollingConfigDeprecationWarner pollingWarner,
    CompactionConfigDeprecationWarner compactionWarner,
    ILoggerFactory loggerFactory) : IStartupAnnouncer
{
    private const string StartupLoggerCategory = "AgentSmith.Server.Startup";

    public void Announce()
    {
        pollingWarner.Warn(config);
        compactionWarner.Warn(config);
        StartupSummaryLogger.Log(config, loggerFactory.CreateLogger(StartupLoggerCategory));
    }
}
