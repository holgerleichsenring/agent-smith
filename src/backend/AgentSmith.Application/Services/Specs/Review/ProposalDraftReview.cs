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

    /// <summary>2026-11-09-9631b: the turn this draft came in on was already to review it.</summary>
    public bool WasWaiting { get; set; }

    /// <summary>2026-11-09-9631b: this turn's reviewers could not read what it names.</summary>
    public bool Unread { get; private set; }

    public string SpecId => Draft.SpecId;

    /// <summary>2026-11-09-9631b: not reviewed, and holding the approval for one further turn. A repository
    /// that can never be read must not take the Approve button away for good: the finding stays and
    /// a person decides, and a run reviews again before it builds.</summary>
    public void MarkUnread()
    {
        Unread = true;
        Stopped = true;
        NotReviewed = !WasWaiting;
    }

    /// <summary>Records this round with the residues it left, the merged answer taken from <see cref="Merged"/>.</summary>
    public void Record(int round, IReadOnlyList<SpecReviewResidue> residues)
    {
        Residues = residues;
        Rounds.Add(new ProposalReviewRound(
            SpecId, round, Merged!.Verdict!, Verdicts, Merged.Cuts, Merged.Findings, residues));
    }
}
