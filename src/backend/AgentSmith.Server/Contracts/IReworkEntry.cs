using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Services.Rework;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// 2026-10-08-e8b9b: the ONE door a rework act goes through, from the ticket or a pull request:
/// decide whether the act asks for a new attempt, make the ticket claimable, start one attempt.
/// </summary>
public interface IReworkEntry
{
    Task<ReworkOutcome> EnterAsync(
        ResolvedProject project, string ticketId, ReworkAct act, string pipeline, CancellationToken cancellationToken);
}
