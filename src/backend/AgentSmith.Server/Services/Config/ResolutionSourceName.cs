using AgentSmith.Contracts.Models.Configuration.Resolved;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-09-22-6968: the wire name of a resolution provenance. One spelling, because the
/// dashboard reads it as a closed vocabulary and two projections now produce it — the
/// effective settings and the counterfactual inherited ones.
/// </summary>
public static class ResolutionSourceName
{
    public static string Of(ResolutionSource source) => source switch
    {
        ResolutionSource.ProjectOverride => "override",
        ResolutionSource.RunResolved => "run-resolved",
        ResolutionSource.CodeDefault => "code-default",
        _ => "global-default",
    };
}
