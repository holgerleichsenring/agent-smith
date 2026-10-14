using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762g: one spec's review between rounds — the text the next round sees, its evidence,
/// what the last round left standing, and whether the one rewrite was spent.</summary>
internal sealed class SpecReviewState(SpecDraft draft, SpecRoundEvidenceResult evidence)
{
    public SpecDraft Draft { get; set; } = draft;
    public SpecRoundEvidenceResult Evidence { get; set; } = evidence;
    public IReadOnlyList<SpecReviewResidue> Residues { get; set; } = [];
    public SpecReviewMerged? Merged { get; set; }
    public int Round { get; set; }
    public bool Rederived { get; set; }
    public string? Failure { get; set; }

    public SpecReviewOutcome Settled() => new(Draft, Round, null, [], Residues);

    public SpecReviewOutcome Standing(string? budgetStop = null, string? failure = null) =>
        new(Draft, Round, Merged?.Verdict ?? SpecReviewOutcome.NotTaken, Merged?.Findings ?? [], Residues,
            budgetStop, failure);
}
