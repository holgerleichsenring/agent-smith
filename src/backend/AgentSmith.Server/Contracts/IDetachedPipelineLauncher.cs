namespace AgentSmith.Server.Contracts;

/// <summary>2026-10-08-10b0: starts a pull-request run nobody waits for — project and pipeline named, no ticket.</summary>
public interface IDetachedPipelineLauncher
{
    Task LaunchAsync(string project, string pipeline, Dictionary<string, object>? context);

    /// <summary>2026-10-09-af10: the same, telling <paramref name="finished"/> whether the run succeeded.</summary>
    Task LaunchAsync(string project, string pipeline, Dictionary<string, object>? context, Func<bool, Task> finished);
}
