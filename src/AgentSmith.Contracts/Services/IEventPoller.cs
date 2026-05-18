using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0140c: per-tracker poller. Pulls all open tickets from one tracker, routes each
/// through IEnvelopeProjectResolver, and calls ISpawnPipelineRunsUseCase per matched
/// project. PollAsync returns a PollResult count summary; the actual spawn happens
/// inside the poller (no longer relayed by PollerHostedService via ClaimRequests).
/// </summary>
public interface IEventPoller
{
    /// <summary>Tracker type label for logs (e.g. "GitHub", "Jira").</summary>
    string PlatformName { get; }

    /// <summary>Tracker catalog name — identifies which tracker connection this poller serves.</summary>
    string TrackerName { get; }

    /// <summary>Polling cadence in seconds. From TrackerConnection.Polling.IntervalSeconds.</summary>
    int IntervalSeconds { get; }

    Task<PollResult> PollAsync(CancellationToken cancellationToken);
}
