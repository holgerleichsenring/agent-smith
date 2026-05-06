using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for querying the project knowledge base wiki.
/// </summary>
public sealed record QueryKnowledgeContext(
    string Question,
    string WikiPath,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
