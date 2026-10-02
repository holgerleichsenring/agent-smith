namespace AgentSmith.Server.Services.Init;

/// <summary>
/// 2026-10-02-5f89d: what the init-state read found — whether the name is a configured
/// project at all, and if so its live init run, or null when none is in flight.
/// </summary>
public sealed record InitRunStateLookup(bool IsKnownProject, InitRunStateResponse? Live)
{
    public static InitRunStateLookup UnknownProject { get; } = new(false, null);
}
