using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for delivering findings via IOutputStrategy without a repository.
/// Used by api-security-scan and other repo-less pipelines.
/// </summary>
public sealed record DeliverFindingsContext(
    IReadOnlyList<string> OutputFormats,
    string? OutputDir,
    PipelineContext Pipeline) : ICommandContext;
