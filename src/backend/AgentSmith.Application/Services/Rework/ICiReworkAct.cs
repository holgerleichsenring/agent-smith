using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Rework;

/// <summary>2026-11-17-3435c: the red-CI act the rework worker weighs beside a ticket's other acts.</summary>
public interface ICiReworkAct
{
    Task<PendingReworkAct?> NewestAsync(ResolvedProject project, string ticketId, PreviousAttempt since, CancellationToken ct);
}
