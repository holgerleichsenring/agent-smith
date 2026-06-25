using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Builds the composed <see cref="DiscoveryQuery"/> for one tracker from config — one branch
/// per routed project's per-tracker trigger, plus the parking statuses a broad branch excludes.
/// </summary>
public interface ITrackerDiscoveryQueryBuilder
{
    DiscoveryQuery Build(AgentSmithConfig config, TrackerConnection tracker);
}
