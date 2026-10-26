namespace AgentSmith.Server.Services.Events.Projection;

/// <summary>
/// 2026-10-26-0f19c: whether this replica is the one that writes run events to the store, and which
/// tenure it is in. Every replica drains every run stream for its own dashboard clients; only the
/// holder of the projection lease persists, because the projections were built for one writer — two
/// replicas that check for a row and then write it both write it. A new tenure means the store may
/// be behind the stream, so each run is caught up once per tenure before its next event is written.
/// </summary>
public sealed class ProjectionLeadership
{
    private long _tenure;
    private volatile bool _holding;

    public bool IsHolding => _holding;

    public long Tenure => Interlocked.Read(ref _tenure);

    public void Begin()
    {
        Interlocked.Increment(ref _tenure);
        _holding = true;
    }

    public void End() => _holding = false;
}
