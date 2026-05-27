using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for compiling discussion findings without a repository.
/// Used by api-security-scan and other repo-less pipelines.
/// </summary>
public sealed record CompileFindingsContext(
    PipelineContext Pipeline) : ICommandContext;
