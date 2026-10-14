using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762j: what the deriver sends back about a parsed cut's series contract — the parser's refusal of
/// it, or, on a re-cut whose previous cut carried one, a reply that dropped it. A hand-back carries no specs and
/// so no contract, and is never asked for one.
/// </summary>
internal static class DerivedContractRule
{
    public const string Dropped = "the previous cut carried a series contract and this reply has none — "
        + "repeat the series contract, amended as needed";

    public static string? Problem(SpecDerivationParser.Parsed parsed, SpecSet? previous)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        if (parsed.ContractProblem is { } problem) return problem;
        return parsed.Derivation is { Set: { IsHandedBack: false, Contract: null } } && previous?.Contract is not null
            ? Dropped : null;
    }
}
