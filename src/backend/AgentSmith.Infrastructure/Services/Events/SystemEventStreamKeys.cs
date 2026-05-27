namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// Redis key conventions for the system event backbone (p0173a). Single
/// stream key, no per-source fanout — the <c>Source</c> field on every
/// SystemEvent lets the dashboard filter client-side without multiplying
/// Redis keys.
///
/// Reuses <see cref="EventStreamKeys.StreamMaxLen"/> +
/// <see cref="EventStreamKeys.StreamTtl"/> so the system stream shares the
/// same retention envelope as run streams — operators see "yesterday's
/// activity" symmetrically across run + system surfaces.
/// </summary>
public static class SystemEventStreamKeys
{
    public const string Stream = "system:events";
}
