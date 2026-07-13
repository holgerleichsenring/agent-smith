using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0324: reachability probes for agent-smith's own datastores, behind the infra
/// preflight check. The server composition probes its shared Redis multiplexer and
/// the relational DB (including pending migrations); the CLI composition probes what
/// a one-shot run can see and reports the rest as unavailable-with-reason so the
/// check skips honestly instead of failing on infrastructure the CLI never uses.
/// Probe methods never throw.
/// </summary>
public interface IPreflightInfraProbe
{
    /// <summary>Null when Redis can be probed here; otherwise the skip reason.</summary>
    string? RedisUnavailableReason { get; }

    /// <summary>Null when the DB can be probed here; otherwise the skip reason.</summary>
    string? PersistenceUnavailableReason { get; }

    Task<ConnectionProbeResult> ProbeRedisAsync(CancellationToken cancellationToken);

    Task<ConnectionProbeResult> ProbePersistenceAsync(CancellationToken cancellationToken);
}
