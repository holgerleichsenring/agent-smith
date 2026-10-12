namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the ways a run ends instead of guessing. Each parks the ticket through
/// the p0318 open-questions path rather than inventing scope; the case code drives
/// the routing and the reason is for the human reading the ticket comment, never
/// compared mechanically.
/// </summary>
/// <param name="Quote">The ticket sentence a REFUSAL objects to, verbatim; null for
/// every other case. A typed slot rather than prose inside the reason, so the comment
/// can quote it as a quote.</param>
/// <param name="Readings">The readings a QUESTION offers — at least two; empty for every
/// other case. Typed because the framework names the taken one twice, in the question
/// and in the notice that the run proceeded on it, and the second run holds only the
/// model's second phrasing.</param>
/// <param name="Taken">Index into <paramref name="Readings"/> of the reading the run
/// takes when nobody answers.</param>
public sealed record SpecHandback(
    SpecHandbackCase Case,
    string Reason,
    string? Quote = null,
    IReadOnlyList<string>? Readings = null,
    int Taken = 0)
{
    /// <summary>
    /// NotImplementable is a verdict, not a question: it parks in its own status,
    /// does not auto-retry on a comment, and restarts only on an explicit Retry.
    /// </summary>
    public bool IsVerdict => Case == SpecHandbackCase.NotImplementable;

    /// <summary>The readings a question offers; empty for every other case.</summary>
    public IReadOnlyList<string> Readings { get; init; } = Readings ?? [];

    /// <summary>2026-10-12-3a36b: the framework-minted evidence lines a CONTRADICTION rests on,
    /// resolved from the ids the model cited; empty for every other case and for a reply that
    /// cited nothing. A contradiction is a claim about the code, and a claim nobody can trace
    /// to a look is how a grep line became a park that later runs replayed.</summary>
    public IReadOnlyList<string> Cites { get; init; } = [];

    /// <summary>The reading the run takes when nobody answers; null unless this is a question.</summary>
    public string? TakenReading =>
        Case == SpecHandbackCase.Question && Taken >= 0 && Taken < Readings.Count
            ? Readings[Taken]
            : null;
}

/// <summary>
/// p0393a: the enumerable hand-back cases. Unresolved points are recorded as
/// ASSUMPTIONS inside the phase, not as a park signal — parking on anything
/// unresolved would fire on nearly every ticket and teach the operator to ignore
/// the signal. A CASE CODE, so a non-progressing loop can be recognised
/// mechanically across runs instead of by diffing LLM-written prose.
/// The values are persisted as integers (<see cref="SpecSetPointer.LastHandbackCase"/>):
/// append, never renumber.
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

    /// <summary>
    /// The ticket demands something that must not be done. Raised by the scope call,
    /// before a sandbox, a credential or a tool exists; parks where a person can answer,
    /// and a repeat with nothing new said parks again rather than continuing.
    /// </summary>
    Refused = 3,

    /// <summary>
    /// The ticket reads two ways and the work differs between them; only the author can
    /// settle it. Parks where a person can answer, naming both readings and the one the
    /// run takes if nobody does — and an unanswered re-trigger proceeds on that one.
    /// </summary>
    Question = 4,

    /// <summary>
    /// 2026-09-22-6ad7: the ticket was filed from an approved specification and that
    /// specification is not readable on its branch — nothing to work, and nothing to guess
    /// from. Not a verdict about the work and not a question about the ticket: the fix is to
    /// put the specs on the branch. Parks where a person can answer, and a re-trigger once
    /// they are there is the answer.
    /// </summary>
    SpecificationMissingFromBranch = 5,

    /// <summary>
    /// 2026-10-12-d142: the work could not be divided between the repositories in scope — no cut
    /// names exactly one repository per spec. Nothing contradicts the code; only a person can say
    /// where each part belongs. Raised by the framework only, never accepted from a model reply;
    /// parks where a person can answer, and a re-trigger after the answer cuts again.
    /// </summary>
    ScopeUnassigned = 6,

    /// <summary>
    /// 2026-10-12-f5f7f: the series' repositories carry different revisions of it — a write that
    /// landed in some of them only. Nothing is derived from or worked over a mixed state; only a
    /// person can say which revision stands. Raised by the framework only; parks where a person can
    /// answer, and a re-cut demand with a re-trigger, or a new approval filed again, is the way out.
    /// </summary>
    SeriesOutOfStep = 7,
}
