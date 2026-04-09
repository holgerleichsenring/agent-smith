using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for compiling run history into a project knowledge base wiki.
/// </summary>
public sealed record CompileKnowledgeContext(
    Repository Repository,
    bool FullRecompile,
    PipelineContext Pipeline) : ICommandContext;
