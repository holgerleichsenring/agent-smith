using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89b: checks one tracker type against its host — host, identity, scope — yielding
/// a step at a time; the open-ticket count is <see cref="ITicketCountCapability"/>'s.
/// </summary>
public interface ITrackerDraftCheck
{
    TrackerType Type { get; }

    IAsyncEnumerable<DraftCheckStep> RunAsync(
        TrackerConnection tracker, string token, CancellationToken cancellationToken);
}
