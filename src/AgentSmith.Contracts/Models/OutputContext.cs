using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Context passed to output strategies for delivery.
/// OutputDir is resolved once by the handler — strategies use it directly.
/// Carries SkillObservations as the universal pipeline output (p0123); strategies
/// that need a stricter shape (SARIF, SpawnFix) convert at their own boundary.
/// </summary>
public sealed record OutputContext(
    string ProjectName,
    string? PrIdentifier,
    IReadOnlyList<SkillObservation> Observations,
    string? ReportMarkdown,
    string OutputDir,
    PipelineContext Pipeline);
