using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for checking convergence across all role contributions in the discussion.
/// </summary>
public sealed record ConvergenceCheckContext(
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
