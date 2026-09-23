namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: asks whether the agent inside a sandbox is still writing its
/// heartbeat, so a held sandbox is verified before a turn reads through it.
/// <para>
/// The alternative is a ninety-second trap: a step pushed at a dead container is a Redis
/// list push that succeeds regardless, followed by a poll that runs to the step timeout
/// plus a thirty-second grace before anything may call it dead. The heartbeat key is
/// written every two seconds, expires in ten and is deleted on a clean exit — one read,
/// and it tells a crash from an orderly shutdown.
/// </para>
/// <para>
/// A probe that cannot read answers NO: spawning a fresh sandbox is slow and correct,
/// while reading through a corpse is fast and wrong.
/// </para>
/// </summary>
public interface ISandboxHeartbeatProbe
{
    /// <summary>True when the agent of this job id is alive right now.</summary>
    Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken);
}
