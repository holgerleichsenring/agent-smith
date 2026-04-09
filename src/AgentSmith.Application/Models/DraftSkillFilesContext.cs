using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for drafting SKILL.md, agentsmith.md, and source.md files for approved candidates.
/// </summary>
public sealed record DraftSkillFilesContext(
    IReadOnlyList<SkillEvaluation> Evaluations,
    PipelineContext Pipeline) : ICommandContext;
