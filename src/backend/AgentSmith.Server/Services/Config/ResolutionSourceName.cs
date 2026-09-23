using AgentSmith.Contracts.Models.Configuration.Resolved;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-09-22-6968: the wire name of a resolution provenance. One spelling, because the
/// dashboard reads it as a closed vocabulary and two projections now produce it — the
/// effective settings and the counterfactual inherited ones.
/// <para>
/// 2026-09-23-2446: EVERY value is named explicitly and an unrecognised one throws. The
/// catch-all arm this used to end in rendered any new value as the process-wide default's
/// name, so a leg added to be told apart from the global one would have claimed to BE it —
/// silently, in the one place an operator goes to find out where a value came from.
/// <c>ProvenanceNames_EveryResolutionSource_HasANameOfItsOwn</c> fails the moment a value
/// is added without a name, which is why throwing here can never reach a running server.
/// </para>
/// </summary>
public static class ResolutionSourceName
{
    public static string Of(ResolutionSource source) => source switch
    {
        ResolutionSource.GlobalDefault => "global-default",
        ResolutionSource.ProjectOverride => "override",
        ResolutionSource.RunResolved => "run-resolved",
        ResolutionSource.CodeDefault => "code-default",
        ResolutionSource.EnvironmentVariable => "environment-variable",
        _ => throw new ArgumentOutOfRangeException(
            nameof(source), source, "This provenance has no wire name of its own."),
    };
}
