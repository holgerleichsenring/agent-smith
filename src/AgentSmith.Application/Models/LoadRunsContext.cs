using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for loading recent run history from .agentsmith/runs/.
/// </summary>
public sealed record LoadRunsContext(
    Repository Repository,
    int LookbackRuns,
    PipelineContext Pipeline) : ICommandContext;
