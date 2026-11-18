namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: terminal status of a sub-agent. <see cref="Succeeded"/> if the
/// agentic loop returned a non-error response; <see cref="Failed"/> for
/// loop exceptions, name-validation rejections, budget exhaustion. A
/// failed child does NOT cancel siblings — the master sees the status
/// and decides what to do. <see cref="Empty"/>: it ran, cost money and
/// answered nothing, so it proves nothing. <see cref="Stopped"/>
/// (2026-11-18-c8e2a): its master's loop ended while it still worked, so it was stopped.
/// </summary>
public enum SubAgentStatus
{
    Succeeded = 0,
    Failed = 1,
    Empty = 2,
    Stopped = 3,
}
