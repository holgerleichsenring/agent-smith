using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-12-92e9b: one design turn's review results, keyed by the exact text each pass reviewed
/// (<see cref="ProposalReviewTexts"/>) — whichever pass ran it, the review_proposal tool or the pass after the loop.
/// The newest result for a text wins. A text in the ledger is not attacked again this turn, and the turn's reply waits
/// until every text of its final draft is in here.
/// </summary>
public sealed class ProposalReviewLedger
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OutcomeProposal> _results = new(StringComparer.Ordinal);

    /// <summary>The turn's ledger, created on first use.</summary>
    public static ProposalReviewLedger Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        lock (pipeline)
        {
            if (pipeline.TryGet<ProposalReviewLedger>(ContextKeys.ProposalReviewLedger, out var ledger) && ledger is not null)
                return ledger;
            var created = new ProposalReviewLedger();
            pipeline.Set(ContextKeys.ProposalReviewLedger, created);
            return created;
        }
    }

    /// <summary>Records a pass's reviewed proposal under every text it holds.</summary>
    public void Record(OutcomeProposal reviewed)
    {
        ArgumentNullException.ThrowIfNull(reviewed);
        lock (_gate)
            foreach (var key in ProposalReviewTexts.Of(reviewed)) _results[key] = reviewed;
    }

    /// <summary>Whether every text of <paramref name="proposal"/> has a result in this turn.</summary>
    public bool Covers(OutcomeProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        lock (_gate) return ProposalReviewTexts.Of(proposal).All(_results.ContainsKey);
    }

    /// <summary>The newest reviewed proposal that held exactly this draft, or null.</summary>
    public OutcomeProposal? For(SpecDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        lock (_gate) return _results.GetValueOrDefault(ProposalReviewTexts.Draft(draft));
    }

    /// <summary><paramref name="proposal"/> carrying the newest result for each of its texts; every text must be covered.</summary>
    public OutcomeProposal Compose(OutcomeProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        lock (_gate)
        {
            var parts = ProposalReviewTexts.Of(proposal).Select(key => (Key: key, Result: _results[key])).ToList();
            return ProposalReviewSlices.Compose(proposal, parts);
        }
    }
}
