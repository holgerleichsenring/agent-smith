using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36b: asks a fresh instance whether a contradiction hand-back holds, before the
/// ticket is parked on it. A port for the reason <see cref="IPhasePremiseChecker"/> is one: the
/// FRAMEWORK makes this call on its own behalf, so a harness stands it down rather than letting it
/// draw an answer meant for the deriver.
/// </summary>
public interface IContradictionChecker
{
    /// <param name="cited">The derivation's own looks the hand-back cites, to be re-read.</param>
    /// <param name="look">The checker's own look, minted under its own letter.</param>
    Task<ContradictionCheck> CheckAsync(
        SpecHandback handback, IReadOnlyList<EvidenceLook> cited, DerivationLook look, string key,
        AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken);
}
