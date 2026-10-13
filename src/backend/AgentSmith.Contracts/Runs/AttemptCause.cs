namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-13-9a59d: why an attempt began — its channel (<see cref="AttemptChannels"/>), the person
/// who acted where one is known, the act's own time by the host's clock, and where it was made.
/// </summary>
public sealed record AttemptCause(
    string Channel, string? Actor = null, DateTimeOffset? At = null, AttemptSource? Source = null);
