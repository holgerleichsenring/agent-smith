namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: the per-assertion verdict for one fixture — matched / missed per
/// GOLD assertion, hallucinated per unmatched DRAFT assertion. Deliberately
/// not a similarity score (spec decision): a missed-assertion list is a work
/// item an operator can act on, a 0.83 is not.
/// </summary>
public sealed record ExpectationJudgeVerdict(
    IReadOnlyList<ExpectationJudgeVerdict.GoldJudgement> Gold,
    IReadOnlyList<string> Hallucinated)
{
    public sealed record GoldJudgement(string Assertion, bool Matched, string? MatchedDraftAssertion);

    public int MatchedCount => Gold.Count(g => g.Matched);
    public int MissedCount => Gold.Count(g => !g.Matched);
}
