using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Commands.Contexts;

/// <summary>
/// Context for executing a plan via AI agent agentic loop.
/// </summary>
public sealed record AgenticExecuteContext(
    Plan Plan,
    Repository Repository,
    string CodingPrinciples,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
