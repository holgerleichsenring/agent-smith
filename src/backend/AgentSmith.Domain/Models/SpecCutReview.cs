namespace AgentSmith.Domain.Models;

/// <summary>
/// p0422: what a fresh instance found wrong with the CUT, before a token is spent
/// building it.
/// <para>
/// Ticket 19106 was cut into one phase carrying both the ticket's own "Step 1 —
/// Inventory (before touching any code)" and its migration steps, so the phase demanded
/// "no production source file is modified" AND "the old library appears nowhere". The
/// master spent two hours before noticing and asking. Reviewing the cut costs one call.
/// </para>
/// </summary>
public sealed record SpecCutReview(IReadOnlyList<CutFinding> Findings, string? Problem = null)
{
    public static SpecCutReview Clean { get; } = new([]);

    /// <summary>A review that could not be taken blocks nothing — it is not evidence of a fault.</summary>
    public bool Deliverable => Findings.Count == 0;
}
