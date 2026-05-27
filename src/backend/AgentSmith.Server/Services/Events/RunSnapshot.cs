using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// Compact overview-view of a run as the broadcaster sees it. One snapshot
/// per active run lives in JobsBroadcaster's active map; finished runs move
/// to the recent ring buffer. Fields are the dashboard contract for the
/// JobUpserted SignalR message.
/// </summary>
public sealed record RunSnapshot(
    string RunId,
    string Pipeline,
    string Trigger,
    IReadOnlyList<string> Repos,
    string Status,
    string? PrUrl,
    string? Summary,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int Sandboxes,
    int StepIndex,
    string? StepName,
    int TotalSteps,
    string? LastEventType,
    decimal CostUsd,
    int LlmCalls)
{
    public static RunSnapshot Empty(string runId) => new(
        runId, "unknown", "unknown", Array.Empty<string>(),
        "running", null, null,
        DateTimeOffset.UtcNow, null, 0, 0, null, 0, null,
        CostUsd: 0m, LlmCalls: 0);

    public RunSnapshot Apply(RunEvent runEvent) => runEvent switch
    {
        RunStartedEvent e => this with
        {
            Pipeline = e.Pipeline, Trigger = e.Trigger, Repos = e.Repos,
            Status = "running", StartedAt = e.StartedAt, LastEventType = e.Type.ToString()
        },
        RunFinishedEvent e => this with
        {
            Status = e.Status, PrUrl = e.PrUrl, Summary = e.Summary,
            FinishedAt = e.FinishedAt, LastEventType = e.Type.ToString()
        },
        SandboxCreatedEvent => this with
        {
            Sandboxes = Sandboxes + 1, LastEventType = runEvent.Type.ToString()
        },
        StepStartedEvent e => this with
        {
            StepIndex = e.StepIndex, StepName = e.StepName, TotalSteps = e.TotalSteps,
            LastEventType = e.Type.ToString()
        },
        StepFinishedEvent e => this with
        {
            LastEventType = e.Type.ToString()
        },
        // p0175-fix: LLM cost rolls up onto the run snapshot so the
        // /system CostRollupCard can read it from the overview without
        // a separate cross-stream subscription. Per-event granularity
        // is preserved in the run-stream; snapshot keeps the running
        // total for fast dashboard reads.
        LlmCallFinishedEvent e => this with
        {
            CostUsd = CostUsd + (decimal)e.CostUsd,
            LlmCalls = LlmCalls + 1,
            LastEventType = e.Type.ToString()
        },
        _ => this with { LastEventType = runEvent.Type.ToString() }
    };
}
