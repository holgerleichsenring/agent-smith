namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-09-01-85b2: what came of resolving one finding's citation — and the difference a
/// null return could not carry.
/// <para>
/// "I could not build evidence for this" and "this location does not exist" used to look
/// the same to the caller, and the caller deletes the second one. A cited line beyond the
/// end of a file it really names is the first kind: the file is there, the window is not,
/// and the finding must pass through untouched rather than be dropped as an invention.
/// </para>
/// </summary>
public sealed record CandidateResolution(CandidateFinding? Candidate, bool CitationExists)
{
    /// <summary>Evidence was found and the finding can be put to a refuter.</summary>
    public static CandidateResolution Refutable(CandidateFinding candidate) => new(candidate, true);

    /// <summary>The cited location is nowhere in the evidence the scan holds — an invention.</summary>
    public static CandidateResolution Invented { get; } = new(null, false);

    /// <summary>
    /// The location is real but no evidence can be shown for it. Nothing is asked and
    /// nothing is dropped.
    /// </summary>
    public static CandidateResolution NoEvidence { get; } = new(null, true);
}
