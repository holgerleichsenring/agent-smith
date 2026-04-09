using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for querying the project knowledge base wiki.
/// </summary>
public sealed record QueryKnowledgeContext(
    string Question,
    string WikiPath,
    PipelineContext Pipeline) : ICommandContext;
