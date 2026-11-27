using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Contracts;

/// <summary>2026-11-27-3532a: the claim request the capacity-queue pump makes for its head, as it stands at the claim.</summary>
public interface IQueueHeadClaim
{
    /// <summary>The head's stored request, carrying the approved set the store holds now.</summary>
    Task<ClaimRequest> AtClaimAsync(CapacityQueueEntry head, ResolvedProject project, CancellationToken ct);
}
