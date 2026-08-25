namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0465: the orphan reaper's decision about one container, with the facts the log
/// line needs. A verdict per candidate keeps the SKIP lines that made the p0465
/// incident readable while the decision itself stays a pure function.
/// </summary>
public sealed record SandboxReapVerdict(
    string ContainerId,
    string JobId,
    string RunId,
    TimeSpan Age,
    SandboxReapOutcome Outcome);
