using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0380: context for loading the experiential-memory index
/// (.agentsmith/memory/MEMORY.md) into the pipeline at plan time.
/// </summary>
public sealed record LoadMemoryIndexContext(
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
