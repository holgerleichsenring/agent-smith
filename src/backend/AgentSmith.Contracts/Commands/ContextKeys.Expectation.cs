namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0328 expectation-negotiation keys. The draft is published BEFORE the
/// durable ask so a checkpointed run restores it on resume instead of
/// re-drafting (the resume must be LLM-free and deterministic — it re-enters
/// the asking step, p0327 shape).
/// </summary>
public static partial class ContextKeys
{
    /// <summary>p0328: the drafted <c>ExpectationDraft</c> (pre-ratification).
    /// Set by NegotiateExpectationHandler before the durable ask; its presence
    /// on re-entry means "already drafted — do not call the model again".</summary>
    public const string ExpectationDraft = "ExpectationDraft";

    /// <summary>p0328: the <c>RatifiedExpectation</c> — the run's acceptance
    /// contract. Read by GeneratePlan/AgenticMaster ({ExpectationSection}),
    /// WriteRunResult and CommitAndPR (assertion checklist).</summary>
    public const string RunExpectation = "RunExpectation";

    /// <summary>p0328: bool marker that the expectation ticket comment was
    /// already posted — a resumed run re-enters the negotiation step and must
    /// not post the block twice.</summary>
    public const string ExpectationCommentPosted = "ExpectationCommentPosted";
}
