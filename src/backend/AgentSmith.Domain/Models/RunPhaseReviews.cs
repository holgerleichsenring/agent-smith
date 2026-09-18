namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-09-17-042eh: every phase's review report, in the order the phases ran — the same
/// shape and the same reason as <see cref="RunAccounts"/>.
/// <para>
/// The per-phase key clears when the next phase is selected, and the pull request is built
/// once at the end of the run. Without a ledger the body would carry the last phase's
/// findings and call them the run's.
/// </para>
/// </summary>
public sealed record RunPhaseReviews(IReadOnlyList<PhaseReviewEntry> Phases)
{
    public static RunPhaseReviews Empty { get; } = new([]);

    /// <summary>Replaces the phase's entry, so a re-reviewed phase counts once.</summary>
    public RunPhaseReviews With(string phaseId, PhaseReviewReport report) =>
        new([.. Phases.Where(p => p.PhaseId != phaseId), new PhaseReviewEntry(phaseId, report)]);

    public IReadOnlyList<PhaseFinding> AllFindings => [.. Phases.SelectMany(p => p.Report.Findings)];
}

/// <summary>One phase's review, under the phase it belongs to.</summary>
public sealed record PhaseReviewEntry(string PhaseId, PhaseReviewReport Report);
