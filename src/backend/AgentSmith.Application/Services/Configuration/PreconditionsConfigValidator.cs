using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-11-16-f3fbc: the preconditions block at validation, as <see cref="SeriesConfigValidator"/> checks series — each
/// problem a Blocking finding naming its field, the one rule <see cref="PreconditionsConfig.Problems"/> states.
/// </summary>
public static class PreconditionsConfigValidator
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Preconditions.Problems().Select(problem => new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, problem, Field: "preconditions"));
    }
}
