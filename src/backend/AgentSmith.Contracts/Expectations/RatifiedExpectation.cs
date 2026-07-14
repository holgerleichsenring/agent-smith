namespace AgentSmith.Contracts.Expectations;

/// <summary>
/// p0328: the ratified expectation — the run's acceptance contract. Carried in
/// the pipeline context (ContextKeys.RunExpectation), injected into plan/master
/// prompts as {ExpectationSection}, rendered as a PR-body checklist, and
/// persisted per run (RunExpectation row) via ExpectationRatifiedEvent.
/// Outcome vocabulary: <see cref="ExpectationOutcomes"/>.
/// </summary>
public sealed record RatifiedExpectation(
    ExpectationDraft Draft,
    string Outcome,
    string RatifiedBy,
    DateTimeOffset RatifiedAt,
    int EditDistance)
{
    public bool IsUnratified => Outcome == ExpectationOutcomes.Unratified;
}
