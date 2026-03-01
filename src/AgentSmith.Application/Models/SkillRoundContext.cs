using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for a single skill round contribution in the plan discussion.
/// </summary>
public sealed record SkillRoundContext(
    string SkillName,
    int Round,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
