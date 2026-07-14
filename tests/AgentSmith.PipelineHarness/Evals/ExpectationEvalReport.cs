namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: one eval run over the fixture set — the persisted artifact per
/// skills/model version (header carries both), so a drafting-prompt change in
/// the skills repo shows its effect as a report DIFF, not an anecdote.
/// Aggregates are derived, never stored: matched/missed over all gold
/// assertions, hallucinated over all draft assertions.
/// </summary>
public sealed record ExpectationEvalReport(
    string ModelId,
    string SkillsPin,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ExpectationEvalReport.FixtureEntry> Entries)
{
    /// <summary>One fixture's outcome: a verdict, or the drafter's loud
    /// failure (a fixture the model cannot draft against is itself a finding,
    /// counted as all-gold-missed in the aggregate).</summary>
    public sealed record FixtureEntry(
        string FixtureId, int GoldAssertions,
        ExpectationJudgeVerdict? Verdict, string? DraftError);

    public int TotalGold => Entries.Sum(e => e.GoldAssertions);
    public int Matched => Entries.Sum(e => e.Verdict?.MatchedCount ?? 0);
    public int Missed => TotalGold - Matched;
    public int Hallucinated => Entries.Sum(e => e.Verdict?.Hallucinated.Count ?? 0);
    public double MatchedRate => TotalGold == 0 ? 0 : (double)Matched / TotalGold;
}
