namespace AgentSmith.Server.Services.Init;

/// <summary>
/// p0489: the launcher's answer. A started launch hands back the run id so the
/// dashboard links straight to it; an already-running launch hands back the LIVE
/// run's id (pressing again opens that run instead of starting a second); the
/// remaining refusals hand back the reason they were refused.
/// </summary>
public sealed record InitLaunchResult(InitLaunchOutcome Outcome, string? RunId, string? Reason)
{
    public static InitLaunchResult Started(string runId) =>
        new(InitLaunchOutcome.Started, runId, null);

    public static InitLaunchResult AlreadyRunning(string runId) =>
        new(InitLaunchOutcome.AlreadyRunning, runId,
            $"An initialization is already running (run {runId}).");

    public static InitLaunchResult NoCapacity(string reason) =>
        new(InitLaunchOutcome.NoCapacity, null, reason);

    public static InitLaunchResult UnknownProject(string project) =>
        new(InitLaunchOutcome.UnknownProject, null, $"Project '{project}' is not configured.");
}
