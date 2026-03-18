using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Context passed to output strategies for delivery.
/// </summary>
public sealed record OutputContext(
    string ProjectName,
    string? PrIdentifier,
    IReadOnlyList<Finding> Findings,
    string? ReportMarkdown,
    PipelineContext Pipeline);
