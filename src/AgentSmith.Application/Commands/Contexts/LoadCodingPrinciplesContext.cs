using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Commands.Contexts;

/// <summary>
/// Context for loading coding principles from a markdown file.
/// </summary>
public sealed record LoadCodingPrinciplesContext(
    string FilePath,
    PipelineContext Pipeline) : ICommandContext;
