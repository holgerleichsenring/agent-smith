namespace AgentSmith.Domain.Models;

/// <summary>
/// One criterion of a derived phase as the spec review left it, with the evidence the
/// review is worth nothing without.
/// <para>
/// A review that reports a judgement starts an argument; one that reports the search it
/// ran and what it returned reports a fact. <see cref="Observation"/> and
/// <see cref="Output"/> are that fact, and they are what a hand-back carries to a ticket
/// author who is not in the room. A finding that cannot fill them is not demonstrated and
/// is not a finding.
/// </para>
/// </summary>
/// <param name="Observation">The search the review ran over the repository, verbatim.</param>
/// <param name="Output">What that search returned — the half a reader can check.</param>
/// <param name="Replacement">
/// For a finding: the criterion this one should become. A correction applies it; a hand-back
/// quotes it so the author has something to accept rather than a complaint to answer.
/// </param>
public sealed record CriterionReview(
    string Criterion,
    SpecReviewDisposition Disposition,
    string? Observation = null,
    string? Output = null,
    string? Note = null,
    string? Replacement = null)
{
    /// <summary>A defect the review demonstrated. Undemonstrated rows — a disposition with no
    /// observation behind it — are not findings, whatever the model called them.</summary>
    public bool IsFinding =>
        Disposition is not SpecReviewDisposition.Decidable
        && !string.IsNullOrWhiteSpace(Observation);

    /// <summary>The criterion can be corrected without changing what "done" means: a shape
    /// criterion or an unobservable one is replaced by the observation that decides it.
    /// <para>
    /// Two findings are deliberately NOT correctable. An already-true one is reported and
    /// left alone, because removing it would quietly shrink the contract. And a finding that
    /// named no <see cref="Replacement"/> has nothing to apply — objecting is not the same as
    /// knowing what should stand instead, and inventing the difference is exactly the licence
    /// this correction is closed against.
    /// </para></summary>
    public bool IsCorrectable =>
        IsFinding
        && Disposition is not SpecReviewDisposition.AlreadyTrue
        && !string.IsNullOrWhiteSpace(Replacement);
}
