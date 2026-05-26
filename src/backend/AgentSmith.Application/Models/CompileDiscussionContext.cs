using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for compiling a multi-agent discussion into a markdown document.
/// </summary>
public sealed record CompileDiscussionContext(
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
