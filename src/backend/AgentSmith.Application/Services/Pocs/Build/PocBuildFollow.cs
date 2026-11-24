using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: follows a build run on its host — read every build.poll_seconds until it is no longer pending, up
/// to the build's deadline (queue + build + scan + push minutes); past it the step fails naming the deadline, why and
/// the keys. The host's pipeline keeps running; a retry follows it through the stored run id.
/// </summary>
public sealed class PocBuildFollow(TimeProvider clock)
{
    public async Task<(PipelineRunState? State, string? Problem)> FollowAsync(
        IPipelineRunner runner, string runId, PocBuildJob job, CiReadRequest bounds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(job);
        var deadline = clock.GetUtcNow().AddMinutes(job.Build.DeadlineMinutes());
        while (true)
        {
            var state = await runner.ReadAsync(runId, bounds, ct);
            if (state.State != CiStates.Pending) return (state, null);
            if (clock.GetUtcNow() >= deadline) return (null, Late(job, state.Url));
            await Task.Delay(TimeSpan.FromSeconds(job.Build.PollSeconds), clock, ct);
        }
    }

    private static string Late(PocBuildJob job, string url) =>
        $"the build did not finish within {job.Build.DeadlineMinutes()} minutes, the sum of "
        + $"{Contracts.Models.Configuration.PocBuildConfig.Key}.queue_minutes, build_timeout_minutes, scan_timeout_minutes and "
        + $"push_timeout_minutes of target '{job.Target.Name}': the host never finished the build; see {url}";
}
