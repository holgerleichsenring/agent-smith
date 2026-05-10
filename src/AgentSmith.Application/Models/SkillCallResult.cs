namespace AgentSmith.Application.Models;

/// <summary>
/// Output contract for <c>ISkillCallRuntime.ExecuteAsync</c>. Carries the five-state
/// outcome plus context (Output, Cost, Trace, FailureReason). Translation between
/// SkillCallResult and the pipeline-level CommandResult is the calling handler's
/// concern (lands in p0127 alongside the consumer migration).
/// </summary>
public sealed record SkillCallResult
{
    public required SkillCallOutcome Outcome { get; init; }
    public string? Output { get; init; }
    public required CallCostRecord Cost { get; init; }
    public required IReadOnlyList<LoopTraceEntry> Trace { get; init; }
    public string? FailureReason { get; init; }
}
