using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-09-17-042eh: every spec's review report, in the order the specs ran — the same
/// shape and the same reason as <see cref="RunAccounts"/>.
/// <para>
/// The per-spec key clears when the next spec is selected, and the pull request is built
/// once at the end of the run. Without a ledger the body would carry the last spec's
/// findings and call them the run's.
/// </para>
/// </summary>
public sealed record RunPhaseReviews([property: AcceptedName("Specs")] IReadOnlyList<PhaseReviewEntry> Phases)
{
    public static RunPhaseReviews Empty { get; } = new([]);

    /// <summary>Replaces the spec's entry, so a re-reviewed spec counts once.</summary>
    public RunPhaseReviews With(string specId, PhaseReviewReport report) =>
        new([.. Phases.Where(p => p.PhaseId != specId), new PhaseReviewEntry(specId, report)]);

    public IReadOnlyList<PhaseFinding> AllFindings => [.. Phases.SelectMany(p => p.Report.Findings)];
}

/// <summary>One spec's review, under the spec it belongs to.</summary>
public sealed record PhaseReviewEntry([property: AcceptedName("SpecId")] string PhaseId, PhaseReviewReport Report);
