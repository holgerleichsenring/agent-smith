namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-18e3: the inventory the scan master already performs, made into an artefact
/// the run can check.
/// <para>
/// The master's first phase has always been told to enumerate the entry points, the trust
/// boundaries and where credentials are handled. Nothing collected that enumeration, so a
/// scan could read the middleware next door, grep for the very configuration key involved,
/// never open the class where the caller's identity is derived, and say nothing about the
/// gap. This is the collection: per entry group, the six stations of a request, each
/// located against what the scan really read or explicitly not.
/// </para>
/// <para>
/// It is a REPORTING surface. It is rendered beside the findings and it raises findings of
/// its own, but it never reaches the ledger the delivery gate reads: a station unlocatable
/// for want of an input the scan was never given would be outstanding forever, and that
/// would fail every scan of every repository without a client or an ownership model.
/// </para>
/// </summary>
public sealed record RequestStationMap(IReadOnlyList<EntryGroupStations> Groups)
{
    public static RequestStationMap Empty { get; } = new([]);

    public bool IsEmpty => Groups.Count == 0;

    /// <summary>Every station no group located, carrying the group it belongs to.</summary>
    public IReadOnlyList<(string Group, StationLocation Station)> Unlocated =>
        [.. Groups.SelectMany(g => g.Unlocated.Select(s => (g.Group, s)))];
}
