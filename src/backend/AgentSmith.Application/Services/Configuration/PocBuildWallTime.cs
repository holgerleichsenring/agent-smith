using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-11-23-933dd: a poc-deploy run waits for its build inside the run wall clock, so a target whose build deadline
/// (queue + build + scan + push minutes) plus the code scan's pocs.scan.timeout_seconds does not fit below the ceiling
/// is a blocking finding naming the target, both sums and the keys — the series block is the precedent.
/// 2026-11-23-c81ec: the ceiling is pocs.run_wall_time_seconds (a PoC run's own budget), and the sum gains the rollout
/// follow's pocs.rollout.deadline_seconds and pocs.rollout.commit_allowance_seconds (sealing and the GitOps commit run
/// between the build and the follow). The time before the build step is not in the sum, so near the ceiling the
/// wall-time cancel may still land first; the pipeline then keeps running and a retry follows it.
/// </summary>
public static class PocBuildWallTime
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var pocs = config.Pocs;
        var ceiling = pocs.RunWallTimeSeconds;
        if (ceiling <= 0) yield break;
        var after = (long)pocs.Scan.TimeoutSeconds + pocs.Rollout.DeadlineSeconds + pocs.Rollout.CommitAllowanceSeconds;
        foreach (var target in pocs.Targets)
        {
            var build = target.Build;
            var sum = (build.DeadlineMinutes() * 60L) + after;
            if (sum < ceiling) continue;
            yield return new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
                $"{PocBuildConfig.Key} of target '{target.Name}': queue_minutes + build_timeout_minutes + scan_timeout_minutes "
                + $"+ push_timeout_minutes ({build.DeadlineMinutes()} min = {build.DeadlineMinutes() * 60} s) plus "
                + $"{PocScanConfig.TimeoutSecondsKey} ({pocs.Scan.TimeoutSeconds} s), {PocRolloutConfig.DeadlineSecondsKey} "
                + $"({pocs.Rollout.DeadlineSeconds} s) and {PocRolloutConfig.CommitAllowanceSecondsKey} "
                + $"({pocs.Rollout.CommitAllowanceSeconds} s) is {sum} s, not below {PocsConfig.RunWallTimeSecondsKey} "
                + $"({ceiling} s); a poc-deploy run would be cancelled before its build and rollout could finish. Lower the "
                + "timeouts or raise the budget.", Field: "pocs");
        }
    }
}
