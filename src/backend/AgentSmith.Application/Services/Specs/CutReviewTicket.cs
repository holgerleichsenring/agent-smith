namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-a5a5: what the cut reviewer is shown of a ticket. PRESENCE, not length, decides
/// the third state: an empty string passes a length test, and a review over a draft with no
/// ticket behind it would be offered a coverage verdict against nothing.
/// </summary>
public enum CutReviewTicket
{
    /// <summary>The whole ticket — the coverage verdict may be offered.</summary>
    Whole,

    /// <summary>A fragment — a reviewer cannot tell "never asked" from "not shown".</summary>
    Truncated,

    /// <summary>No ticket at all — there is nothing to compare coverage against.</summary>
    Absent,
}
