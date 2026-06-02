namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// p0201: no-op <see cref="IStepInFlightMarker"/> used by unit tests of
/// <see cref="JobLoop"/> that don't exercise the heartbeat side-effect.
/// </summary>
internal sealed class NullStepInFlightMarker : IStepInFlightMarker
{
    public static readonly NullStepInFlightMarker Instance = new();

    private NullStepInFlightMarker() { }

    public void MarkStepInFlight(bool inFlight) { }
}
