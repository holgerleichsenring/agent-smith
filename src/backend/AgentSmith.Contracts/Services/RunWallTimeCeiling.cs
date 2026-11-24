using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-23-c81ec: the ONE answer to "how long may this run take" — a PoC run (<see cref="RegisteredRunKind.Poc"/>)
/// pocs.run_wall_time_seconds, every other run orchestrator.max_run_wall_time_seconds — with the key that applied, so
/// PipelineRunWatchdog, CancelEnforcer, the run's cancel sentence and the enforcer's summary all name the same budget.
/// </summary>
public sealed record RunWallTimeCeiling(int Seconds, string Key)
{
    public const string OrchestratorKey = "orchestrator.max_run_wall_time_seconds";

    /// <summary>The ceiling of a run of <paramref name="kind"/> under <paramref name="config"/>.</summary>
    public static RunWallTimeCeiling Of(AgentSmithConfig config, RegisteredRunKind kind)
    {
        ArgumentNullException.ThrowIfNull(config);
        return kind == RegisteredRunKind.Poc
            ? new RunWallTimeCeiling(config.Pocs.RunWallTimeSeconds, PocsConfig.RunWallTimeSecondsKey)
            : new RunWallTimeCeiling(config.Orchestrator.MaxRunWallTimeSeconds, OrchestratorKey);
    }

    /// <summary>The ceiling of a run of the preset <paramref name="pipeline"/>.</summary>
    public static RunWallTimeCeiling Of(AgentSmithConfig config, string? pipeline) =>
        Of(config, PipelinePresets.RunKindOf(pipeline ?? string.Empty));

    /// <summary>The run's cancel sentence: the budget in minutes and its key.</summary>
    public string CancelSentence => $"Run exceeded its {Seconds / 60}-minute wall-time budget ({Key}) and was cancelled.";
}
