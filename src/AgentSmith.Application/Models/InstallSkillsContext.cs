using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for installing approved skills to their target directory.
/// </summary>
public sealed record InstallSkillsContext(
    IReadOnlyList<SkillEvaluation> ApprovedSkills,
    string DraftDirectory,
    string InstallPath,
    PipelineContext Pipeline) : ICommandContext;
