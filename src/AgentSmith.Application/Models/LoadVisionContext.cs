using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for loading the project vision from .agentsmith/project-vision.md.
/// </summary>
public sealed record LoadVisionContext(
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
