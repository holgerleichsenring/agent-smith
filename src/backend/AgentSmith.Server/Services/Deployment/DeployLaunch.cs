namespace AgentSmith.Server.Services.Deployment;

/// <summary>2026-11-18-3863c: how a deploy launch ended — the run id, or the sentence that refused it.</summary>
public sealed record DeployLaunch(DeployLaunchOutcome Outcome, string? RunId, string? Reason)
{
    public static DeployLaunch Started(string runId) => new(DeployLaunchOutcome.Started, runId, null);

    public static DeployLaunch Refused(string reason) => new(DeployLaunchOutcome.Refused, null, reason);

    public static DeployLaunch UnknownProject(string project) =>
        new(DeployLaunchOutcome.UnknownProject, null, $"no project '{project}' is configured");
}
