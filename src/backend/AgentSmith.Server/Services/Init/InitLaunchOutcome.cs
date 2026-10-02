namespace AgentSmith.Server.Services.Init;

/// <summary>
/// p0489: what came of an operator's request to initialize a project. One
/// started outcome and four refusals — each refusal carries the reason the
/// button renders inline, and the button stays pressable.
/// </summary>
public enum InitLaunchOutcome
{
    /// <summary>The run was admitted, recorded and enqueued.</summary>
    Started,

    /// <summary>An init of this project is already in flight — its run id is the answer.</summary>
    AlreadyRunning,

    /// <summary>The run's footprint does not fit right now; nothing was reserved.</summary>
    NoCapacity,

    /// <summary>No project of that name is configured.</summary>
    UnknownProject,

    /// <summary>2026-10-02-5f89d: another launch of this project holds the launch lock right
    /// now; its outcome is the answer, so this one starts nothing.</summary>
    BeingStarted,
}
