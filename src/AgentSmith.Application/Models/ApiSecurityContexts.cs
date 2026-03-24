using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

public sealed record LoadSwaggerContext(
    PipelineContext Pipeline) : ICommandContext;

public sealed record SpawnNucleiContext(
    PipelineContext Pipeline) : ICommandContext;

public sealed record ApiSecurityTriageContext(
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;

public sealed record ApiSecuritySkillRoundContext(
    string SkillName,
    int Round,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
