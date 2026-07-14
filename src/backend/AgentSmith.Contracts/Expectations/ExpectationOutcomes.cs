namespace AgentSmith.Contracts.Expectations;

/// <summary>
/// p0328: ratification-outcome vocabulary, persisted on the RunExpectation row.
/// The raw material for the p0329 first-PR-acceptance metric: verbatim/edited
/// (+ edit distance) measure how close the draft was to what the human wanted;
/// 'unratified' is the visible degradation stamp of a headless auto-ratify or a
/// ratification timeout — never a silent skip.
/// </summary>
public static class ExpectationOutcomes
{
    public const string Verbatim = "verbatim";
    public const string Edited = "edited";
    public const string Rejected = "rejected";
    public const string Unratified = "unratified";
}
