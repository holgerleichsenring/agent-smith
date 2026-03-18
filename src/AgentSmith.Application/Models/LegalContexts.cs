using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for acquiring a document from a local folder source.
/// </summary>
public sealed record AcquireSourceContext(
    SourceConfig Config,
    PipelineContext Pipeline) : ICommandContext;

/// <summary>
/// Context for converting a document to Markdown and detecting its type.
/// </summary>
public sealed record BootstrapDocumentContext(
    Repository Repository,
    AgentConfig Agent,
    string SkillsPath,
    PipelineContext Pipeline) : ICommandContext;

/// <summary>
/// Context for writing the analysis output and archiving the source document.
/// When OutputFormat is set, delegates to the matching IOutputStrategy.
/// </summary>
public sealed record DeliverOutputContext(
    SourceConfig Config,
    Repository Repository,
    PipelineContext Pipeline,
    string? OutputFormat = null) : ICommandContext;
