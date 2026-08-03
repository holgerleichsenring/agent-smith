namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the two ways derivation ends the run instead of guessing. Both park the
/// ticket through the p0318 open-questions path rather than inventing scope; the
/// case code drives the routing and the reason is for the human reading the ticket
/// comment, never compared mechanically.
/// </summary>
public sealed record SpecHandback(SpecHandbackCase Case, string Reason)
{
    /// <summary>
    /// NotImplementable is a verdict, not a question: it parks in its own status,
    /// does not auto-retry on a comment, and restarts only on an explicit Retry.
    /// </summary>
    public bool IsVerdict => Case == SpecHandbackCase.NotImplementable;
}

/// <summary>
/// p0393a: the enumerable hand-back cases. Unresolved points are recorded as
/// ASSUMPTIONS inside the phase, not as a park signal — parking on anything
/// unresolved would fire on nearly every ticket and teach the operator to ignore
/// the signal. A CASE CODE, so a non-progressing loop can be recognised
/// mechanically across runs instead of by diffing LLM-written prose.
/// </summary>
public enum SpecHandbackCase
{
    /// <summary>No hand-back — the normal case.</summary>
    None = 0,

    /// <summary>
    /// The ticket is readable but contradicts what the repository actually is.
    /// Only findable after AnalyzeCode, which is why derivation runs there and
    /// not at fetch time.
    /// </summary>
    RequirementsContradictRepository = 1,

    /// <summary>A VERDICT, not a question: this cannot be built as asked.</summary>
    NotImplementable = 2,
}
