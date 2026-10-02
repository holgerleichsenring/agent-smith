using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89b: counts a tracker's open tickets reading at most one page, and says
/// "at least N" when more exist. A tracker type without one reports that counting is not
/// supported — never zero, because an empty listing does not mean no tickets.
/// </summary>
public interface ITicketCountCapability
{
    TrackerType Type { get; }

    Task<DraftCheckStep> CountOpenAsync(
        TrackerConnection tracker, string token, CancellationToken cancellationToken);
}
