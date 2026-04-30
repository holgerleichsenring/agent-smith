using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for generating an execution plan via AI agent.
/// </summary>
public sealed record GeneratePlanContext(
    Ticket Ticket,
    ProjectMap ProjectMap,
    string CodingPrinciples,
    AgentConfig AgentConfig,
    PipelineContext Pipeline,
    string? CodeMap = null,
    string? ProjectContext = null) : ICommandContext;
