using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// p0140c: warns when a project declares the deprecated project-level Polling block. Polling
/// moved to TrackerConnection so N projects sharing one tracker pull once per interval instead
/// of N times. The project-level block is still parsed but ignored by the per-tracker pollers;
/// operators get a structured warning per affected project at config load.
/// </summary>
public sealed class PollingConfigDeprecationWarner(ILogger<PollingConfigDeprecationWarner> logger)
{
    public void Warn(AgentSmithConfig config)
    {
        foreach (var (name, project) in config.Projects)
        {
            if (!project.Polling.Enabled) continue;
            logger.LogWarning(
                "Project '{Project}' declares deprecated project-level polling (enabled=true, "
                + "interval={Interval}s). Move the polling: block to tracker '{Tracker}'. "
                + "The project-level block is ignored by the per-tracker pollers.",
                name, project.Polling.IntervalSeconds, project.Tracker.Name);
        }
    }
}
