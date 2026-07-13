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
    string? TicketId = null,
    // p0275: the pipeline's KNOWN ordered step labels (CommandDisplayNames for
    // the resolved preset). The dashboard seeds its step rail from this so the
    // step list is a stable skeleton from t=0 — early steps no longer vanish when
    // their StepStarted event is evicted from the 2000-event run buffer. Null for
    // pre-p0275 events / unknown presets → the dashboard falls back to event-only.
    IReadOnlyList<string>? PlannedSteps = null,
    // p0320c: resolved project name + tracker platform, persisted onto the Run
    // row so the projector's TOCTOU backstop (RunFinished status="queued") can
    // upsert a capacity-queue entry from the row's own fields. Null for pre-
    // p0320c events and non-project (CLI ephemeral) runs.
    string? Project = null,
    string? Platform = null,
    // p0330: the spawner's container/pod handle (JOB_ID of a spawned orchestrator).
    // Persisted onto the Run row so the server-side cancel enforcer can force-kill
    // the k8s Job / Docker container by runId — the event stream is the ONLY channel
    // a spawned orchestrator has back to the server DB. Null for in-process runs.
    string? JobId = null)
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
