using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>2026-11-23-31dci: the holds block at validation — each problem a Blocking finding naming its field, the one
/// rule <see cref="HoldsConfig.Problems"/> states.</summary>
public static class HoldsConfigValidator
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Holds.Problems().Select(problem => new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, problem, Field: "holds"));
    }
}
