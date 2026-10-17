namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the safeguard behind p0357's ruling that a summary may never stand in
/// for the manual. It is an ACCOUNTING, not a coverage score: every ticket segment
/// is either carried by a named spec or listed as discarded with a reason. A
/// percentage gets optimised against the moment it is measured; a list is
/// checkable by a human in seconds in the pull request.
/// <para>
/// The accounting spans the UNION of the specs — a segment is carried if ANY
/// spec carries it.
/// </para>
/// </summary>
public sealed record SpecAccounting(
    IReadOnlyList<CarriedSegment> Carried,
    IReadOnlyList<DiscardedSegment> Discarded,
    IReadOnlyList<int> Unaccounted,
    IReadOnlyList<DiscardedContext>? DiscardedContexts = null)
{
    public static SpecAccounting Empty { get; } = new([], [], []);

    /// <summary>2026-09-08-1830: the contexts the scope call named that the derivation
    /// left out, each with its reason. Empty on a cut from a catalog that names none.</summary>
    public IReadOnlyList<DiscardedContext> DiscardedContexts { get; init; } = DiscardedContexts ?? [];

    /// <summary>2026-10-17-293ba: the segments the derivation declared every spec needs. A record of
    /// the declared audience only — coverage comes from <see cref="Carried"/>, which already holds
    /// each shared segment once per spec that received it. Empty on a cut that declared none.</summary>
    public IReadOnlyList<int> Shared { get; init; } = [];

    /// <summary>
    /// True when every segment of the ticket is spoken for. A false here does NOT
    /// split at all: the run falls back to one spec with the full ticket pinned,
    /// which is a shape known to work, rather than partially applying one that is not.
    /// </summary>
    public bool IsComplete => Unaccounted.Count == 0;
}

/// <summary>p0393a: a ticket segment carried by a named spec.</summary>
public sealed record CarriedSegment(int SegmentId, string SpecId);

/// <summary>p0393a: a ticket segment deliberately left out, with the reason a reviewer reads.</summary>
public sealed record DiscardedSegment(int SegmentId, string Reason);
