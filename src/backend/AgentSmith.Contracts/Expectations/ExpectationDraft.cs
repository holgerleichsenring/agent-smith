namespace AgentSmith.Contracts.Expectations;

/// <summary>
/// p0328: the schema-capped Soll block the agent drafts after AnalyzeCode and
/// the human ratifies before implementation starts. The caps live HERE, on the
/// type, because brevity is the schema's contract: what does not fit in five
/// testable assertions is a design document, and the drafting prompt instructs
/// the model to say so instead of overflowing.
/// </summary>
public sealed record ExpectationDraft(
    string Observed,
    IReadOnlyList<string> Expected,
    IReadOnlyList<string> Constraints,
    ExpectationOpenQuestion? OpenQuestion)
{
    /// <summary>Hard cap on Expected — each entry one verifiable sentence.</summary>
    public const int MaxExpected = 5;

    /// <summary>Hard cap on Constraints.</summary>
    public const int MaxConstraints = 3;

    /// <summary>Soft sentence cap: an "assertion" longer than this is prose,
    /// not a testable statement.</summary>
    public const int MaxAssertionLength = 300;
}
