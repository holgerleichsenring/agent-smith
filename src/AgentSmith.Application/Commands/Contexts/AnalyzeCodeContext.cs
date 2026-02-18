using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Commands.Contexts;

/// <summary>
/// Context for analyzing a repository's structure and dependencies.
/// </summary>
public sealed record AnalyzeCodeContext(
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
