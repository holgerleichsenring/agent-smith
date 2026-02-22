using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for generating an execution plan via AI agent.
/// </summary>
public sealed record GeneratePlanContext(
    Ticket Ticket,
    CodeAnalysis CodeAnalysis,
    string CodingPrinciples,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
