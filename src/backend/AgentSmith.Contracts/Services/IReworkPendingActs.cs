using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-08-0781: the newest trusted rework act on a ticket after <paramref name="since"/>'s
/// cutoff — a keyword comment, or a standing request for changes on one of its pull requests.
/// </summary>
public interface IReworkPendingActs
{
    Task<PendingReworkAct?> NewestAsync(ResolvedProject project, string ticketId, PreviousAttempt since, CancellationToken cancellationToken);
}
