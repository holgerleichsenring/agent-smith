using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for auto-bootstrapping a repository with a .context.yaml file.
/// Detects project type and generates CCS if not already present.
/// </summary>
public sealed record BootstrapProjectContext(
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
