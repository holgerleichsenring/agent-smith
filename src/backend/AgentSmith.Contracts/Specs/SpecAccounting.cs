namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the safeguard behind p0357's ruling that a summary may never stand in
/// for the manual. It is an ACCOUNTING, not a coverage score: every ticket segment
/// is either carried by a named phase or listed as discarded with a reason. A
/// percentage gets optimised against the moment it is measured; a list is
/// checkable by a human in seconds in the pull request.
/// <para>
/// The accounting spans the UNION of the phases — a segment is carried if ANY
/// phase carries it.
/// </para>
/// </summary>
public sealed record SpecAccounting(
    IReadOnlyList<CarriedSegment> Carried,
    IReadOnlyList<DiscardedSegment> Discarded,
    IReadOnlyList<int> Unaccounted)
{
    public static SpecAccounting Empty { get; } = new([], [], []);

    /// <summary>
    /// True when every segment of the ticket is spoken for. A false here does NOT
    /// split at all: the run falls back to one phase with the full ticket pinned,
    /// which is a shape known to work, rather than partially applying one that is not.
    /// </summary>
    public bool IsComplete => Unaccounted.Count == 0;
}

/// <summary>p0393a: a ticket segment carried by a named phase.</summary>
public sealed record CarriedSegment(int SegmentId, string PhaseId);

/// <summary>p0393a: a ticket segment deliberately left out, with the reason a reviewer reads.</summary>
public sealed record DiscardedSegment(int SegmentId, string Reason);
