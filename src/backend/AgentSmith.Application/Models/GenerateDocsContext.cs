using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for LLM-based documentation generation for code changes.
/// </summary>
public sealed record GenerateDocsContext(
    Repository Repository,
    IReadOnlyList<CodeChange> Changes,
    string CodingPrinciples,
    AgentConfig AgentConfig,
    PipelineContext Pipeline,
    string? CodeMap = null,
    string? ProjectContext = null) : ICommandContext;
