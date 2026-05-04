using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the RunReviewPhase / RunFinalPhase steps. Both share the same handler
/// (PhaseAdvanceHandler) — the builders set Phase + Round to distinguish.
/// </summary>
public sealed record PhaseAdvanceContext(
    PipelinePhase Phase,
    int Round,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
