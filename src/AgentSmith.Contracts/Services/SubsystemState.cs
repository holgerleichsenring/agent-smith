namespace AgentSmith.Contracts.Services;

/// <summary>
/// Lifecycle state of a server subsystem reported via ISubsystemHealth.
/// /health/ready returns 200 only when every subsystem is Up.
/// </summary>
public enum SubsystemState
{
    Up,
    Degraded,
    Down,
    Disabled
}
