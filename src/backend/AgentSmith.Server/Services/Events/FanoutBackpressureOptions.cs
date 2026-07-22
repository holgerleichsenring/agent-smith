namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0367: per-send bound for <see cref="BackpressureSafeFanout"/>. A single slow
/// or dead client transport must not stall the shared drain loop for every other
/// run and tab, so each fan-out send is abandoned (drop-logged) past this timeout.
/// </summary>
public sealed record FanoutBackpressureOptions
{
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(2);
}
