using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: one draft's review within a design turn — its text, its evidence, its rounds (the
/// pass's own, or carried over), the merged answer, and whether it was reviewed.</summary>
internal sealed class ProposalDraftReview(SpecDraft draft)
{
    public SpecDraft Draft { get; } = draft;
    public SpecRoundEvidenceResult Evidence { get; set; } = SpecRoundEvidenceResult.None;
    public List<ProposalReviewRound> Rounds { get; } = [];
    public SpecReviewMerged? Merged { get; set; }
    public IReadOnlyList<string?> Verdicts { get; set; } = [];
    public bool Stopped { get; set; }
    public bool NotReviewed { get; set; }

    /// <summary>2026-11-10-18cf: why this draft was not reviewed — the run's cost cap, no reviewer answered, or its
    /// reviewers could not read — or null.</summary>
    public string? StoppedBecause { get; set; }

    /// <summary>2026-11-10-18cf: the text the review attacked.</summary>
    public string? AttackedYaml { get; set; }

    public bool AttackedCurrentText => string.Equals(AttackedYaml, Draft.Yaml, StringComparison.Ordinal);

    /// <summary>2026-11-10-8e59: a review ended clean on exactly this text — its last round said build on it.</summary>
    public bool ReviewedClean =>
        !NotReviewed && AttackedCurrentText && Rounds is [.., { Verdict: SpecReviewVerdicts.Build }];

    public string SpecId => Draft.SpecId;

    /// <summary>2026-11-09-9631b: this turn's reviewers could not read what it names.</summary>
    public void MarkUnread() => MarkNotReviewed(ProposalReviewSummary.UnreadWhy);

    public void MarkNotReviewed(string why)
    {
        Stopped = true;
        NotReviewed = true;
        StoppedBecause = why;
    }

    /// <summary>Records the pass's round from <see cref="Merged"/>; nothing is revised, so nothing is left as a residue.</summary>
    public void Record() =>
        Rounds.Add(new ProposalReviewRound(SpecId, Rounds.Count + 1, Merged!.Verdict!, Verdicts, Merged.Cuts, Merged.Findings, []));
}
