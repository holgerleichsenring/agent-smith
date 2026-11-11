using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: one draft's review within a design turn — its last valid text, its evidence, the
/// rounds recorded so far, the last round's merged answer, and whether it stopped or was not reviewed.</summary>
internal sealed class ProposalDraftReview(SpecDraft draft)
{
    public SpecDraft Draft { get; set; } = draft;
    public SpecRoundEvidenceResult Evidence { get; set; } = SpecRoundEvidenceResult.None;
    public List<ProposalReviewRound> Rounds { get; } = [];
    public IReadOnlyList<SpecReviewResidue> Residues { get; set; } = [];
    public SpecReviewMerged? Merged { get; set; }
    public IReadOnlyList<string?> Verdicts { get; set; } = [];
    public bool Stopped { get; set; }
    public bool NotReviewed { get; set; }

    /// <summary>2026-11-10-18cf: why this draft's review has not finished — the run's cost cap, no reviewer
    /// answered, or its reviewers could not read — or null.</summary>
    public string? StoppedBecause { get; set; }

    /// <summary>2026-11-10-18cf: the text the last taken round attacked.</summary>
    public string? AttackedYaml { get; set; }

    /// <summary>2026-11-11-2bdfb: why the re-prompt this draft went into could not be used, or null.</summary>
    public string? NotUsedBecause { get; set; }

    public bool AttackedCurrentText => string.Equals(AttackedYaml, Draft.Yaml, StringComparison.Ordinal);

    /// <summary>2026-11-10-8e59: a review ended clean on exactly this text — its last round said build on it.</summary>
    public bool ReviewedClean =>
        !NotReviewed && AttackedCurrentText && Rounds is [.., { Verdict: SpecReviewVerdicts.Build }];

    public string SpecId => Draft.SpecId;

    /// <summary>2026-11-09-9631b: this turn's reviewers could not read what it names. 2026-11-10-8e59: the draft is
    /// marked not reviewed with that reason, and the review before it is built reads it again.</summary>
    public void MarkUnread()
    {
        Stopped = true;
        NotReviewed = true;
        StoppedBecause = ProposalReviewSummary.UnreadWhy;
    }

    /// <summary>Records this round with the residues it left, the merged answer taken from <see cref="Merged"/>.</summary>
    public void Record(int round, IReadOnlyList<SpecReviewResidue> residues)
    {
        Residues = residues;
        Rounds.Add(new ProposalReviewRound(
            SpecId, round, Merged!.Verdict!, Verdicts, Merged.Cuts, Merged.Findings, residues));
    }
}
