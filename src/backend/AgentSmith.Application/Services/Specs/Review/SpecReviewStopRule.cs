using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: when a spec's review is through — the plugin's own rule. BUILD, or BUILD WITH CUTS with
/// every cut applied: no residue left standing and no problem among the citations of the REVISED text that
/// were checked (an unchecked one is told to the next reviewer, never counted as a pass or a fail). The
/// loop ends too on a round that changed no text, on no reviewer taken, and when the rounds are spent.
/// </summary>
public static class SpecReviewStopRule
{
    public static bool IsDone(
        SpecReviewMerged merged, IReadOnlyList<SpecReviewResidue> residues, SpecRoundEvidenceResult evidenceAfter)
    {
        ArgumentNullException.ThrowIfNull(merged);
        ArgumentNullException.ThrowIfNull(residues);
        ArgumentNullException.ThrowIfNull(evidenceAfter);
        return merged.Verdict == SpecReviewVerdicts.Build
            || (merged.Verdict == SpecReviewVerdicts.BuildWithCuts && residues.Count == 0
                && evidenceAfter.Problems.Count == 0);
    }
}
