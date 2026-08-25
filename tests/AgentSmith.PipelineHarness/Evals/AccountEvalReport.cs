namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-25-7035: one scoring run over the account corpus.
/// <para>
/// TWO rates, each over its own denominator. A false negative is a criterion the branch met
/// and the account refused; a false positive is one it did not meet and the account passed.
/// Reporting a single number, or two numbers over one population, is how a judge gets tuned
/// in whichever direction the last complaint pointed — which is the history this exists to
/// end.
/// </para>
/// <para>
/// Both populations are stated. A rate over four criteria is not a rate, and a report that
/// hides its n invites the first baseline to be read as ground truth.
/// </para>
/// </summary>
public sealed record AccountEvalReport(
    string ModelId,
    string PromptVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AccountEvalReport.FixtureEntry> Entries)
{
    /// <summary>One fixture's outcome, or the loud failure of an account that could not be
    /// taken — which is itself a finding and is never scored as agreement.</summary>
    public sealed record FixtureEntry(
        string FixtureId,
        string Class,
        IReadOnlyList<CriterionOutcome> Criteria,
        string? Problem);

    /// <summary>What the truth was, what the account said, and what that costs.</summary>
    public sealed record CriterionOutcome(
        string Criterion,
        bool TruthIsMet,
        bool AccountSatisfied,
        string? Citation,
        string? Note)
    {
        public bool IsFalseNegative => TruthIsMet && !AccountSatisfied;
        public bool IsFalsePositive => !TruthIsMet && AccountSatisfied;
        public bool Agrees => TruthIsMet == AccountSatisfied;
    }

    private IEnumerable<CriterionOutcome> All => Entries.SelectMany(e => e.Criteria);

    /// <summary>Criteria the branch genuinely met — the denominator of the false-negative
    /// rate, and nothing else.</summary>
    public int MetPopulation => All.Count(c => c.TruthIsMet);

    /// <summary>Criteria the branch genuinely did not meet — the denominator of the
    /// false-positive rate.</summary>
    public int UnmetPopulation => All.Count(c => !c.TruthIsMet);

    public int FalseNegatives => All.Count(c => c.IsFalseNegative);

    public int FalsePositives => All.Count(c => c.IsFalsePositive);

    public double FalseNegativeRate =>
        MetPopulation == 0 ? 0 : (double)FalseNegatives / MetPopulation;

    public double FalsePositiveRate =>
        UnmetPopulation == 0 ? 0 : (double)FalsePositives / UnmetPopulation;

    public IReadOnlyList<string> ClassesCovered =>
        [.. Entries.Select(e => e.Class).Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)];
}
