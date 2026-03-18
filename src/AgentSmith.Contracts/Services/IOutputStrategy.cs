using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Delivers pipeline output in a specific format (SARIF, Markdown, console, file).
/// Implementations are selected via keyed services based on --output parameter.
/// </summary>
public interface IOutputStrategy
{
    string StrategyType { get; }

    Task DeliverAsync(OutputContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Context passed to output strategies for delivery.
/// </summary>
public sealed record OutputContext(
    string ProjectName,
    string? PrIdentifier,
    IReadOnlyList<Finding> Findings,
    string? ReportMarkdown,
    PipelineContext Pipeline);

/// <summary>
/// A single finding from a security scan or analysis.
/// </summary>
public sealed record Finding(
    string Severity,
    string File,
    int StartLine,
    int? EndLine,
    string Title,
    string Description,
    int Confidence);
