namespace AgentSmith.Sandbox.Wire;

public static class RedisKeys
{
    public const string Prefix = "sandbox";

    public static string InputKey(string jobId) => $"{Prefix}:{jobId}:in";
    public static string EventsKey(string jobId) => $"{Prefix}:{jobId}:events";
    public static string ResultsKey(string jobId) => $"{Prefix}:{jobId}:results";
    /// <summary>
    /// p0201: liveness heartbeat key. Sandbox.Agent SETs this every 2s with
    /// EX 10; server-side SandboxLivenessWatcher polls for its presence. TTL
    /// tolerates up to 4 consecutive missed writes (GC pause, brief I/O stall)
    /// without false-positive cancel.
    /// </summary>
    public static string HeartbeatKey(string jobId) => $"{Prefix}:{jobId}:heartbeat";
}
