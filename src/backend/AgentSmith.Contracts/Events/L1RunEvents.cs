namespace AgentSmith.Contracts.Events;

public sealed record RunStartedEvent(
    string RunId,
    string Trigger,
    string Pipeline,
    IReadOnlyList<string> Repos,
    DateTimeOffset StartedAt)
    : RunEvent(RunId, EventType.RunStarted, StartedAt);

/// <summary>
/// p0176b: <see cref="CostUsd"/> carries the pipeline-aggregate cost from
/// PipelineCostTracker.EstimateCostUsd() at run end. Optional — older
/// producers may emit null; consumers fall back to the per-call
/// LlmCallFinished accumulation. Defence in depth against a producer
/// leaking calls past the factory-level event wrap.
/// </summary>
public sealed record RunFinishedEvent(
    string RunId,
    string Status,
    string? PrUrl,
    string Summary,
    DateTimeOffset FinishedAt,
    decimal? CostUsd = null)
    : RunEvent(RunId, EventType.RunFinished, FinishedAt);
