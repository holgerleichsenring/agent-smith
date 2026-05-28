namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: terminal status of a sub-agent. <see cref="Succeeded"/> if the
/// agentic loop returned a non-error response; <see cref="Failed"/> for
/// loop exceptions, name-validation rejections, budget exhaustion. A
/// failed child does NOT cancel siblings — the master sees the status
/// and decides what to do.
/// </summary>
public enum SubAgentStatus
{
    Succeeded = 0,
    Failed = 1,
}
