using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for loading domain rules (coding principles, style guides, etc.) from a file.
/// </summary>
public sealed record LoadCodingPrinciplesContext(
    string RelativePath,
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
