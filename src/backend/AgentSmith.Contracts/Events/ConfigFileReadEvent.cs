namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0173c: emitted when a config file is read by the agent. Carries the
/// path + kind + size so the dashboard can render "what configs did this
/// run actually read". <see cref="RunId"/> is null for startup reads
/// (agentsmith.yml at server boot), populated for run-scoped reads
/// (per-target context.yaml + coding-principles.md inside an active
/// IRunContextAccessor scope).
/// </summary>
public sealed record ConfigFileReadEvent(
    string Source,
    string Path,
    ConfigFileKind Kind,
    long SizeBytes,
    string? RunId,
    DateTimeOffset Timestamp)
    : SystemEvent(Source, SystemEventType.ConfigFileRead, Timestamp);
