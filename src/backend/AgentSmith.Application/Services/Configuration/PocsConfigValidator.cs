using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>2026-11-23-933da: the pocs block at validation — each problem a Blocking finding with Field 'pocs', the one
/// rule <see cref="PocsConfig.Problems"/> states.</summary>
public static class PocsConfigValidator
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Pocs.Problems().Select(problem => new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, problem, Field: "pocs"));
    }
}
