using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>2026-11-17-713ec: the branch_cleanup block at validation — each problem a Blocking finding naming its
/// field, the one rule <see cref="BranchCleanupConfig.Problems"/> states.</summary>
public static class BranchCleanupConfigValidator
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.BranchCleanup.Problems().Select(problem => new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, problem, Field: "branch_cleanup"));
    }
}
