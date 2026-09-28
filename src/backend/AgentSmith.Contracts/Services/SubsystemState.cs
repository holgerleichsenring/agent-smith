namespace AgentSmith.Contracts.Services;

/// <summary>
/// Lifecycle state of a server subsystem reported via ISubsystemHealth.
/// /health lists every subsystem with its state; Down or Degraded marks the server degraded.
/// </summary>
public enum SubsystemState
{
    Up,
    Degraded,
    Down,
    Disabled
}
