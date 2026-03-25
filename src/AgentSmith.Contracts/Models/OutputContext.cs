using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Context passed to output strategies for delivery.
/// OutputDir is resolved once by the handler — strategies use it directly.
/// </summary>
public sealed record OutputContext(
    string ProjectName,
    string? PrIdentifier,
    IReadOnlyList<Finding> Findings,
    string? ReportMarkdown,
    string OutputDir,
    PipelineContext Pipeline);
