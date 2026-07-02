using AgentSmith.Contracts.Providers;

namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// Read-only reachability probes for agent-smith's own datastores — Redis
/// (transport / locks / streams) and the relational persistence DB (run history +
/// single-run constraint). Both are load-bearing for the server.
/// </summary>
public interface IInfraConnectivityProbe
{
    Task<ConnectionProbeResult> ProbeRedisAsync(CancellationToken cancellationToken);

    Task<ConnectionProbeResult> ProbePersistenceAsync(CancellationToken cancellationToken);
}
