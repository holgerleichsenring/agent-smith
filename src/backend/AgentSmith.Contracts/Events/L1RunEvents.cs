namespace AgentSmith.Contracts.Events;

public sealed record RunStartedEvent(
    string RunId,
    string Trigger,
    string Pipeline,
    IReadOnlyList<string> Repos,
    DateTimeOffset StartedAt,
    // p0186: agent name from the resolved project config (e.g. "claude-default",
    // "azure-openai-default"). Optional for backward compat — pre-p0186 events
    // omit it; consumers fall back to "unknown".
    string? AgentName = null,
    // p0211: ticket id from the request, threaded onto the snapshot at run
    // start so the title can fall back to a stable "{pipeline} #{ticketId}"
    // label before (or absent) any TicketFetchedEvent. Null for non-ticket
    // (manual / CLI) runs and pre-p0211 events.
    string? TicketId = null)
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
