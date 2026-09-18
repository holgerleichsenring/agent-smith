using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: asks whether the premises a phase STATES still hold, before its work
/// starts. A port for the same reason <see cref="ISpecCutReviewer"/> is one: it is a call the
/// FRAMEWORK makes on its own behalf, not one a master's script describes, so a harness stands
/// it down rather than letting it draw an answer meant for the master.
/// </summary>
public interface IPhasePremiseChecker
{
    Task<PremiseCheck> CheckAsync(
        PhaseDraft draft, PhasePremises premises, DerivationLook look,
        IReadOnlyList<PhaseProgress> alreadyRan, string key, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken cancellationToken);
}
