namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0367: a coalesced sandbox-activity beat for the Run group. Replaces the
/// per-tool-call firehose with at most one rollup per run per throttle interval —
/// the run-detail view still shows life (N commands, the latest one) without the
/// O(tool-calls) flood. Carried on the distinct "SandboxActivity" hub message so
/// the lifecycle "RunEvent" stream stays O(steps).
/// </summary>
public sealed record SandboxActivityRollup(
    string RunId,
    string Repo,
    int Commands,
    string? LastCommand,
    string? LastSummary,
    DateTimeOffset Timestamp);
