namespace AgentSmith.Contracts.Sandbox;

/// <summary>2026-10-13-9a59i: what a backend reports about its sandbox's main process.</summary>
public enum SandboxExitState
{
    /// <summary>Still running — an ordinary release.</summary>
    Running,

    /// <summary>The process ended; the exit code and OOM flag are evidence.</summary>
    Exited,

    /// <summary>Not found or unreadable — never folded into Running.</summary>
    Gone,
}

/// <summary>
/// 2026-10-13-9a59i: the exit a backend read off its container or pod before releasing it. Only
/// <see cref="SandboxExitState.Exited"/> carries facts; Running and Gone carry none, and are kept
/// apart so a sandbox deleted under the run is not recorded as an ordinary release.
/// </summary>
public sealed record SandboxExitRead(SandboxExitState State, int? ExitCode = null, bool? OomKilled = null)
{
    public static SandboxExitRead Running { get; } = new(SandboxExitState.Running);
    public static SandboxExitRead Gone { get; } = new(SandboxExitState.Gone);

    public static SandboxExitRead Exited(int exitCode, bool oomKilled) =>
        new(SandboxExitState.Exited, exitCode, oomKilled);

    /// <summary>The wire label SandboxDisposedEvent.ExitState carries.</summary>
    public string Label => State switch
    {
        SandboxExitState.Exited => SandboxExitLabels.Exited,
        SandboxExitState.Gone => SandboxExitLabels.Gone,
        _ => SandboxExitLabels.Running,
    };
}

/// <summary>2026-10-13-9a59i: the labels of SandboxDisposedEvent.ExitState; null = no evidence.</summary>
public static class SandboxExitLabels
{
    public const string Running = "running";
    public const string Exited = "exited";
    public const string Gone = "gone";
}
