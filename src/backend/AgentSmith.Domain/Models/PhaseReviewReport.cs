namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-09-17-042eh: what came of one phase's review — including that it was NOT TAKEN.
/// <para>
/// A review that was skipped for a spent cost cap, or whose call failed, or that had no
/// changed file to read, produces the same empty finding list as a review that read the whole
/// diff and objected to nothing. Recorded as a bare list those two are indistinguishable, and
/// a reader — the phase record, the pull request, 2026-09-17-042ej — would report "nothing
/// found" over a question nobody asked. So the ABSENCE carries its reason.
/// </para>
/// </summary>
/// <param name="Reviewed">True when a fresh instance actually read this phase's diff.</param>
/// <param name="Findings">What it kept. Empty on a review that was not taken.</param>
/// <param name="Why">Why it was not taken. Null exactly when <paramref name="Reviewed"/>.</param>
public sealed record PhaseReviewReport(
    bool Reviewed, IReadOnlyList<PhaseFinding> Findings, string? Why = null)
{
    /// <summary>A review that ran, with whatever it kept.</summary>
    public static PhaseReviewReport Taken(IReadOnlyList<PhaseFinding> findings) =>
        new(true, findings ?? []);

    /// <summary>A review that did not happen, and says so.</summary>
    public static PhaseReviewReport NotTaken(string why) => new(false, [], why);

    /// <summary>Nothing recorded at all — the phase has not reached its review.</summary>
    public static PhaseReviewReport None { get; } = new(false, [], null);

    /// <summary>The one sentence a reader gets when the review was not taken.</summary>
    public string NotReviewedNote => $"not reviewed: {Why}";
}
