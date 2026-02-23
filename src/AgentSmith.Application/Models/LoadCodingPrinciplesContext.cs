using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for loading coding principles from a markdown file.
/// </summary>
public sealed record LoadCodingPrinciplesContext(
    string RelativePath,
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
