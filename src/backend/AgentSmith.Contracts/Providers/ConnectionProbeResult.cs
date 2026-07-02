namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Outcome of a read-only connectivity probe against an external provider
/// (repository host or ticket tracker). <see cref="Ok"/> is true when the
/// credentials authenticated and the remote answered; otherwise
/// <see cref="Error"/> carries a short, secret-free reason. <see cref="LatencyMs"/>
/// is the wall-clock time of the round-trip, whether it succeeded or failed.
/// </summary>
public sealed record ConnectionProbeResult(bool Ok, long LatencyMs, string? Error)
{
    public static ConnectionProbeResult Reachable(long latencyMs) => new(true, latencyMs, null);

    public static ConnectionProbeResult Unreachable(long latencyMs, string error) =>
        new(false, latencyMs, error);
}
