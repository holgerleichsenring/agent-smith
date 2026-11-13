using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-12-92e9b: one design turn's review results, keyed by the exact text each pass reviewed
/// (<see cref="ProposalReviewTexts"/>) — whichever pass ran it, the review_proposal tool or the pass after the loop.
/// The newest result for a text wins. A text in the ledger is not attacked again this turn, and the turn's reply waits
/// until every text of its final draft is in here. 2026-11-13-3f79: a text a pass passed (build or build with cuts,
/// <see cref="ProposalReviewText.PassedIn"/>) is settled for the rest of the turn whatever it becomes — applying its
/// cuts does not send it back to review; only a refused or unreviewed text needs a pass over its new text.
/// </summary>
public sealed class ProposalReviewLedger
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OutcomeProposal> _results = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OutcomeProposal> _settled = new(StringComparer.Ordinal);

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

    /// <summary>Records a pass's reviewed proposal under every text it holds, and as settled where it passed one.</summary>
    public void Record(OutcomeProposal reviewed)
    {
        ArgumentNullException.ThrowIfNull(reviewed);
        lock (_gate)
            foreach (var text in ProposalReviewTexts.Of(reviewed))
            {
                _results[text.Key] = reviewed;
                if (text.PassedIn(reviewed)) _settled[text.SettledKey] = reviewed;
            }
    }

    /// <summary>Whether every text of <paramref name="proposal"/> has a result in this turn or is settled.</summary>
    public bool Covers(OutcomeProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        lock (_gate)
            return ProposalReviewTexts.Of(proposal).All(t => _results.ContainsKey(t.Key) || _settled.ContainsKey(t.SettledKey));
    }

    /// <summary>The newest reviewed proposal that held exactly this draft, or null.</summary>
    public OutcomeProposal? For(SpecDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        lock (_gate) return _results.GetValueOrDefault(ProposalReviewTexts.Draft(draft));
    }

    /// <summary>The newest result that passed what <paramref name="settledKey"/> names — a draft's id, an epic's
    /// parent (<see cref="ProposalReviewTexts.SettledParent"/>) or the list — or null when it is not settled.</summary>
    public OutcomeProposal? Settled(string settledKey)
    {
        ArgumentNullException.ThrowIfNull(settledKey);
        lock (_gate) return _settled.GetValueOrDefault(settledKey);
    }

    /// <summary>Every round a pass of this turn left, so what a later pass carries can be told from what it took.</summary>
    public IReadOnlyList<ProposalReviewRound> Rounds()
    {
        lock (_gate) return [.. _results.Values.Concat(_settled.Values).SelectMany(r => r.ReviewRounds)];
    }

    /// <summary><paramref name="proposal"/> carrying the newest result for each of its texts — a settled text with no
    /// exact result its settled one; every text must be covered.</summary>
    public OutcomeProposal Compose(OutcomeProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        lock (_gate)
        {
            var parts = ProposalReviewTexts.Of(proposal).Select(text => _results.TryGetValue(text.Key, out var exact)
                ? (text, exact, false)
                : (text, _settled[text.SettledKey], true)).ToList();
            return ProposalReviewSlices.Compose(proposal, parts);
        }
    }
}
