using System.Diagnostics;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Preconditions;

/// <summary>
/// 2026-11-22-6d85c: the interruptions agent-smith restarts by itself — those that ended since shortly before this process
/// started, so a deploy does not retry every interruption from before the restart existed. One per process; the restart
/// pass and the series row read the same window, so a row says 'restarting' only for an attempt the pass will retry.
/// 2026-11-22-747dc: how long before the start is preconditions.interrupted_restart_window_minutes (0 turns the restart
/// off), read from the configuration in force on every call, so a studio save applies at the next pass. The start is
/// the PROCESS's, not this object's: workers are built after the listener binds, and a reader may come hours later.
/// </summary>
public sealed class InterruptedRestartWindow(
    IConfigurationLoader configLoader, ServerContext serverContext, DateTimeOffset? processStart = null)
{
    /// <summary>When this process started.</summary>
    public DateTimeOffset Started { get; } = processStart ?? new DateTimeOffset(Process.GetCurrentProcess().StartTime.ToUniversalTime());

    /// <summary>The earliest end the restart takes under <paramref name="preconditions"/>; null when it is off.</summary>
    public DateTimeOffset? Since(PreconditionsConfig preconditions)
    {
        ArgumentNullException.ThrowIfNull(preconditions);
        return preconditions.InterruptedRestartWindowMinutes == 0
            ? null : Started - TimeSpan.FromMinutes(preconditions.InterruptedRestartWindowMinutes);
    }

    /// <summary>The preconditions settings in force — read once by a reader that asks <see cref="Covers"/> per row.</summary>
    public PreconditionsConfig InForce() => configLoader.LoadConfig(serverContext.ConfigPath).Preconditions;

    /// <summary>Whether an interruption that ended at <paramref name="finishedAt"/> is restarted under <paramref name="preconditions"/>.</summary>
    public bool Covers(DateTimeOffset? finishedAt, PreconditionsConfig preconditions) =>
        finishedAt is { } at && Since(preconditions) is { } since && at >= since;
}
