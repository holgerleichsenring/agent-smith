using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-11-23-933dd: a poc-deploy run waits for its build inside the run wall clock, so a target whose build deadline
/// (queue + build + scan + push minutes) plus the code scan's pocs.scan.timeout_seconds does not fit below
/// orchestrator.max_run_wall_time_seconds is a blocking finding naming the target, both sums and the keys — the series
/// block is the precedent. The time before the build step is not in the sum, so near the ceiling the watchdog may still
/// cancel first; the pipeline then keeps running and a retry follows it.
/// </summary>
public static class PocBuildWallTime
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var ceiling = config.Orchestrator.MaxRunWallTimeSeconds;
        if (ceiling <= 0) yield break;
        foreach (var target in config.Pocs.Targets)
        {
            var build = target.Build;
            var sum = (build.DeadlineMinutes() * 60L) + config.Pocs.Scan.TimeoutSeconds;
            if (sum < ceiling) continue;
            yield return new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
                $"{PocBuildConfig.Key} of target '{target.Name}': queue_minutes + build_timeout_minutes + scan_timeout_minutes "
                + $"+ push_timeout_minutes ({build.DeadlineMinutes()} min = {build.DeadlineMinutes() * 60} s) plus "
                + $"{PocScanConfig.TimeoutSecondsKey} ({config.Pocs.Scan.TimeoutSeconds} s) is {sum} s, not below "
                + $"orchestrator.max_run_wall_time_seconds ({ceiling} s); a poc-deploy run would be cancelled before its build "
                + "could finish. Lower the timeouts or raise the ceiling.", Field: "pocs");
        }
    }
}
