using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Rework;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// 2026-10-08-e8b9b: whether a ticket sits parked after its last attempt — in done_status or
/// failed_status by its CURRENT status, or held by an unmoved fact — and the trigger status it has to
/// be moved to before a claimer accepts it. Verdict parks are not parked here: Retry brings them back.
/// </summary>
public interface IReworkParkCheck
{
    Task<ReworkPark> CheckAsync(ResolvedProject project, string ticketId, CancellationToken cancellationToken);

    Task<bool> MoveAsync(ResolvedProject project, string ticketId, string status, CancellationToken cancellationToken);
}
