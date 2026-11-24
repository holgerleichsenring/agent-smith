using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Pocs.Acts;

/// <summary>2026-11-23-c81ed: maps a Poc row to what PocActPlan's table reads, for the launcher, the detail page and the
/// import's pending-delete check.</summary>
internal static class PocActSubjects
{
    public static PocActSubject Map(Poc poc)
    {
        ArgumentNullException.ThrowIfNull(poc);
        return new(poc.Id, poc.Name, poc.Target, poc.State, poc.Replicas, poc.Revision, poc.ImageDigest, poc.ReadinessPath);
    }
}
