namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// p0201: tiny seam JobLoop uses to flag "step running on the executor task"
/// to the heartbeat publisher. Decouples JobLoop from the concrete heartbeat
/// transport (Redis) so unit tests can pass a no-op without standing up a
/// connection multiplexer.
/// </summary>
internal interface IStepInFlightMarker
{
    void MarkStepInFlight(bool inFlight);
}
