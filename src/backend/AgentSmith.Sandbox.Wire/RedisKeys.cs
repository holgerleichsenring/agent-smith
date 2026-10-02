namespace AgentSmith.Sandbox.Wire;

public static class RedisKeys
{
    public const string Prefix = "sandbox";

    public static string InputKey(string jobId) => $"{Prefix}:{jobId}:in";
    public static string EventsKey(string jobId) => $"{Prefix}:{jobId}:events";
    public static string ResultsKey(string jobId) => $"{Prefix}:{jobId}:results";

    /// <summary>
    /// 2026-10-02-5ab2f: the sliding expiry of a job's in, results and events keys. Every
    /// write refreshes it, so a live job never loses them; a job whose server or agent died
    /// leaves nothing behind for longer than a day.
    /// </summary>
    public static readonly TimeSpan JobKeyTtl = TimeSpan.FromHours(24);

    /// <summary>
    /// p0201: liveness heartbeat key. Sandbox.Agent SETs this every 2s with
    /// EX 10; server-side SandboxLivenessWatcher polls for its presence. TTL
    /// tolerates up to 4 consecutive missed writes (GC pause, brief I/O stall)
    /// without false-positive cancel.
    /// </summary>
    public static string HeartbeatKey(string jobId) => $"{Prefix}:{jobId}:heartbeat";

    /// <summary>
    /// p0360b: the server's active-run set (mirrors EventStreamKeys.ActiveRunsSet —
    /// Sandbox.Wire cannot reference Infrastructure, so the literal is duplicated and
    /// pinned equal by a test). The agent consults it at its idle limit: a sandbox
    /// whose RUN is still live must keep waiting instead of self-terminating — the
    /// idle exit is a backstop for a DEAD server, and killing a healthy multi-repo
    /// run's idle sandbox killed the whole run.
    /// </summary>
    public const string ActiveRunsSet = "agentsmith:runs:active";
}
