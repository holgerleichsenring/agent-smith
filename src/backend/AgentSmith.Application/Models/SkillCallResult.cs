using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Output contract for <c>ISkillCallRuntime.ExecuteAsync</c>. Carries the five-state
/// outcome plus context (Output, Cost, Trace, FailureReason). Translation between
/// SkillCallResult and the pipeline-level CommandResult is the calling handler's
/// concern (lands in p0127 alongside the consumer migration).
///
/// p0147b: <see cref="RuntimeObservations"/> carries typed observations the
/// runtime synthesizes when a call ends Incomplete or FailedRuntime — execution-
/// limit hits and uncaught exceptions become Info-severity observations with a
/// stable Category prefix. Callers append these to the pipeline observation list
/// so silent skill drops become visible to the operator and downstream skills.
/// </summary>
public sealed record SkillCallResult
{
    public required SkillCallOutcome Outcome { get; init; }
    public string? Output { get; init; }
    public required CallCostRecord Cost { get; init; }
    public required IReadOnlyList<LoopTraceEntry> Trace { get; init; }
    public string? FailureReason { get; init; }
    public IReadOnlyList<SkillObservation> RuntimeObservations { get; init; } = [];

    /// <summary>
    /// p0151b: the set of source-file paths the skill read in this call,
    /// captured by <see cref="LoopTraceCollector"/>. Consumed by the
    /// observation parser to drop hallucinated <c>analyzed_from_source</c>
    /// claims whose cited file was never read.
    /// </summary>
    public IReadOnlyCollection<string> ReadPaths { get; init; } = Array.Empty<string>();
}
