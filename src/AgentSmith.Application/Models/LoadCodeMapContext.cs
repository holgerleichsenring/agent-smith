using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for loading a code-map.yaml from the repository root.
/// </summary>
public sealed record LoadCodeMapContext(
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
