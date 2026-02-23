using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for loading .agentsmith/context.yaml into the pipeline.
/// </summary>
public sealed record LoadContextContext(
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
