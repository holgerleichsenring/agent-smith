using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for acquiring a document from a local folder source.
/// </summary>
public sealed record AcquireSourceContext(
    RepoConnection Config,
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
/// Context for delivering the master's written report through the IOutputStrategy
/// named by <see cref="OutputFormat"/>, into <see cref="OutputDir"/> when it writes a file.
/// </summary>
public sealed record DeliverOutputContext(
    string ProjectName,
    string OutputFormat,
    string? OutputDir,
    PipelineContext Pipeline) : ICommandContext;
