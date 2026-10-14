using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-10-13-8762u: the series block at startup. Each problem is a Blocking finding naming its field — it only
/// reports, since a global finding blocks no project; the first run, epic parse or continuation that reads the
/// value refuses it. An unmeasured block is Advisory: the attempt is not sized against the wall clock.
/// </summary>
public static class SeriesConfigValidator
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        foreach (var problem in config.Series.Problems(config.Orchestrator.MaxRunWallTimeSeconds))
            yield return new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, problem, Field: "series");
        if (config.Series.SecondsPerSpec == 0)
            yield return new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
                "series.seconds_per_spec is 0 (not measured): an attempt runs series.specs_per_attempt specs and is not "
                + "sized against orchestrator.max_run_wall_time_seconds, so the watchdog may end it before its boundary.",
                Field: "series.seconds_per_spec");
    }
}
