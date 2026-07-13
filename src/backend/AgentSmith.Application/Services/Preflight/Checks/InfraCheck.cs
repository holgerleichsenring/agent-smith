using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// p0324: Redis and the relational DB answer where this composition can see them.
/// A down/volatile Redis is not just a queue stall — reapers historically treated an
/// empty runs:active as authoritative and killed live sandboxes (p0238); pending DB
/// migrations fail the persistence probe because the server never migrates itself.
/// Parts the composition cannot probe (CLI one-shot runs) skip with the reason.
/// </summary>
public sealed class InfraCheck(IPreflightInfraProbe infraProbe) : IPreflightCheck
{
    public string Name => "infra";

    public string Category => "infra";

    public async Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        var (redisLine, redisFailure) = await ProbePartAsync(
            "redis", infraProbe.RedisUnavailableReason,
            infraProbe.ProbeRedisAsync, cancellationToken);
        var (dbLine, dbFailure) = await ProbePartAsync(
            "persistence", infraProbe.PersistenceUnavailableReason,
            infraProbe.ProbePersistenceAsync, cancellationToken);

        if (redisFailure || dbFailure)
            return PreflightCheckResult.Fail(
                $"{redisLine}; {dbLine}",
                "Redis: point REDIS_URL at a reachable, durable instance — a flaky/volatile Redis has "
                + "historically made reapers treat live sandboxes as orphans (p0238). Persistence: check "
                + "persistence.connection_string and run 'agentsmith database migrate' before server "
                + "start (pending migrations fail this check).");

        if (infraProbe.RedisUnavailableReason is not null && infraProbe.PersistenceUnavailableReason is not null)
            return PreflightCheckResult.Skip($"{redisLine}; {dbLine}");

        return PreflightCheckResult.Pass($"{redisLine}; {dbLine}");
    }

    private static async Task<(string Line, bool Failed)> ProbePartAsync(
        string label,
        string? unavailableReason,
        Func<CancellationToken, Task<ConnectionProbeResult>> probe,
        CancellationToken cancellationToken)
    {
        if (unavailableReason is not null)
            return ($"{label}: skipped — {unavailableReason}", false);

        var result = await probe(cancellationToken);
        return result.Ok
            ? ($"{label}: ok {result.LatencyMs}ms", false)
            : ($"{label}: {result.Error}", true);
    }
}
