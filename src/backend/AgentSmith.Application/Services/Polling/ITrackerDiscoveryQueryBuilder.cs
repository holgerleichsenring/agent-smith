using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Builds the composed <see cref="DiscoveryQuery"/> for one tracker from config — one branch
/// per routed project's per-tracker trigger, plus the parking statuses a broad branch excludes.
/// <para>
/// 2026-09-25-c1f7: ASYNCHRONOUS, because the query also names the tickets an approved record
/// still expects work on and that answer lives in a store. It was synchronous while every input
/// was configuration.
/// </para>
/// </summary>
public interface ITrackerDiscoveryQueryBuilder
{
    Task<DiscoveryQuery> BuildAsync(
        AgentSmithConfig config, TrackerConnection tracker, CancellationToken cancellationToken);
}
