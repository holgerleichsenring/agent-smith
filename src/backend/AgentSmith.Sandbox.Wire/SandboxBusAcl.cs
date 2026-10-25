namespace AgentSmith.Sandbox.Wire;

/// <summary>
/// 2026-10-25-c5a7e: the Redis ACL of a sandbox's per-job user. It reaches only its own
/// job's keys and reads the active-run set, so a command that reads the agent's
/// credential cannot read, forge or wait on another job's steps. Defined beside
/// <see cref="RedisKeys"/> because the key layout and the command list change together.
/// </summary>
public static class SandboxBusAcl
{
    public const string UserPrefix = "sbx-";

    /// <summary>
    /// The commands the agent issues (RPOP, LPUSH, EXPIRE, XADD, SET/SETEX, DEL, SISMEMBER) plus
    /// what StackExchange.Redis needs to connect.
    /// </summary>
    public static IReadOnlyList<string> Commands { get; } =
    [
        "rpop", "lpush", "expire", "xadd", "set", "setex", "del", "sismember",
        "hello", "auth", "client|setname", "client|setinfo", "ping", "echo",
    ];

    public static string UserName(string jobId) => UserPrefix + jobId;

    /// <summary>The rules after <c>ACL SETUSER {user}</c>; <c>reset</c> first makes a re-issue idempotent.</summary>
    public static IReadOnlyList<string> Rules(string jobId, string password) =>
    [
        "reset", "on", ">" + password,
        $"~{RedisKeys.Prefix}:{jobId}:*",
        $"%R~{RedisKeys.ActiveRunsSet}",
        "resetchannels", "-@all",
        .. Commands.Select(command => "+" + command),
    ];
}
