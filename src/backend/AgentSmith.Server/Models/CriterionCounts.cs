namespace AgentSmith.Server.Models;

/// <summary>
/// Criteria counted by the status they ended on, after the operator's overrules.
/// <para>
/// <see cref="Share"/> is met over met, unmet and unproven. An unproven criterion counts
/// against the share — nobody showed it holds — while a not-applicable one is in neither
/// side: it was never the run's to satisfy. <see cref="Overruled"/> counts the criteria an
/// operator's judgement moved; <see cref="StaleOverrules"/> counts judgements that answered
/// a status the snapshot no longer carries, which are shown and not applied.
/// </para>
/// </summary>
public sealed record CriterionCounts(
    int Met, int Unmet, int Unproven, int NotApplicable, int Overruled, int StaleOverrules)
{
    public static CriterionCounts None { get; } = new(0, 0, 0, 0, 0, 0);

    /// <summary>The share's denominator.</summary>
    public int Judged => Met + Unmet + Unproven;

    /// <summary>Null where nothing was judged: no measurement is not 0%.</summary>
    public double? Share => Judged == 0 ? null : (double)Met / Judged;

    public CriterionCounts Plus(CriterionCounts other) => new(
        Met + other.Met,
        Unmet + other.Unmet,
        Unproven + other.Unproven,
        NotApplicable + other.NotApplicable,
        Overruled + other.Overruled,
        StaleOverrules + other.StaleOverrules);
}
