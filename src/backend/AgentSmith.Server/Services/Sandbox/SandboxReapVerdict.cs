namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0465: the reaper's decision about one sandbox, with the facts the log line needs.
/// A verdict per candidate keeps the SKIP lines that made the p0465 incident readable
/// while the decision itself stays a pure function.
/// 2026-09-22-2d11a: the id is a container id on Docker and a pod name on Kubernetes,
/// and the conversation label rides along so a held sandbox says whose hold spared it.
/// </summary>
public sealed record SandboxReapVerdict(
    string SandboxId,
    string JobId,
    string RunId,
    string ConversationId,
    TimeSpan Age,
    SandboxReapOutcome Outcome);
