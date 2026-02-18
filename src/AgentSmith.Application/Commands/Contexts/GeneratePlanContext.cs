using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Commands.Contexts;

/// <summary>
/// Context for generating an execution plan via AI agent.
/// </summary>
public sealed record GeneratePlanContext(
    Ticket Ticket,
    CodeAnalysis CodeAnalysis,
    string CodingPrinciples,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
