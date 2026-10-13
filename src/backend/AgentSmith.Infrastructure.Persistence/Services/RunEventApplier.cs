using AgentSmith.Infrastructure.Persistence.Contracts;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Maps a typed RunEvent onto its ER entities over a SCOPED unit of work. Pure projection — no
/// buffering, no Redis. The raw-event trail is the projector's concern; this applies only the
/// events that carry structured run facts the dashboard reads.
/// <para>2026-10-13-9a59d: a dispatcher. Each <see cref="IRunEventFamily"/> names the events it
/// applies; an event reaches every family that names it, in registration order, and an event no
/// family names is trail-only.</para>
/// <para>2026-08-25-61f1: an event arrives with the position it holds in its run's trail, and every
/// projection that INSERTS stamps it on the row — the row's identity, so the store can refuse to hold
/// one event's record twice.</para>
/// </summary>
public sealed class RunEventApplier
{
    private readonly IReadOnlyDictionary<Type, IRunEventFamily[]> _routes;

    public RunEventApplier(IEnumerable<IRunEventFamily> families)
    {
        var list = families.ToList();
        _routes = list.SelectMany(f => f.Events).Distinct()
            .ToDictionary(t => t, t => list.Where(f => f.Events.Contains(t)).ToArray());
    }

    /// <summary>The families an event of <paramref name="eventType"/> reaches, in order.</summary>
    public IReadOnlyList<IRunEventFamily> FamiliesOf(Type eventType) =>
        _routes.TryGetValue(eventType, out var families) ? families : [];

    /// <summary>
    /// Applies an event whose trail position is not known — the terminal reconciler's path, which
    /// runs outside the drain that mints positions. Nothing it applies inserts a child row, so no
    /// identity is lost by its absence.
    /// </summary>
    public Task ApplyAsync(IUnitOfWork uow, AgentSmith.Contracts.Events.RunEvent ev, CancellationToken ct) =>
        ApplyAsync(uow, ev, null, ct);

    public async Task ApplyAsync(
        IUnitOfWork uow, AgentSmith.Contracts.Events.RunEvent ev, long? eventSeq, CancellationToken ct)
    {
        foreach (var family in FamiliesOf(ev.GetType()))
            await family.ApplyAsync(uow, ev, eventSeq, ct);
    }
}
