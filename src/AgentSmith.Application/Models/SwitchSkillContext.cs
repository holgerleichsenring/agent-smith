using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for switching the active skill/role in the pipeline.
/// </summary>
public sealed record SwitchSkillContext(
    string SkillName,
    PipelineContext Pipeline) : ICommandContext;
