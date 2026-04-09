using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for asking human approval for each evaluated skill candidate.
/// </summary>
public sealed record ApproveSkillsContext(
    IReadOnlyList<SkillEvaluation> Evaluations,
    string? DraftDirectory,
    PipelineContext Pipeline) : ICommandContext;
