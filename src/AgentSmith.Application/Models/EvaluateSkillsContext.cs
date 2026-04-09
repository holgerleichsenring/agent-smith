using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for evaluating discovered skill candidates for fit and safety.
/// </summary>
public sealed record EvaluateSkillsContext(
    IReadOnlyList<SkillCandidate> Candidates,
    IReadOnlyList<string> InstalledSkillNames,
    PipelineContext Pipeline) : ICommandContext;
