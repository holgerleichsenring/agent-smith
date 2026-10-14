using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: a review round's changes applied by the design partner. The drafts with cuts or a REFUSE
/// still admitted by <see cref="ProposalRoundGate"/> go into ONE re-prompt (<see cref="ProposalCutsNudge"/>);
/// the re-proposal is merged by <see cref="ProposalReProposalGuard"/> and published; each draft gets its
/// residues, its re-checked evidence and its round, and stops on the stop rule or on unchanged text. A failed
/// call, an unusable reply or one that changed no draft ends the review on the last valid proposal.
/// </summary>
public sealed class ProposalRoundRevision(
    ProposalCutsReprompt reprompt, ProposalReProposalGuard guard, SpecCutResidues residues, ISpecRoundEvidence evidence,
    ILogger<ProposalRoundRevision> logger)
{
    /// <summary>False when the review ends here.</summary>
    internal async Task<bool> ApplyAsync(
        ProposalReviewSession session, IReadOnlyList<ProposalDraftReview> toFix, CancellationToken ct)
    {
        var applying = ProposalRoundGate.Admit(session, toFix);
        foreach (var draft in toFix.Except(applying)) Unchanged(session, draft);
        if (applying.Count == 0) return true;
        var reply = await reprompt.RunAsync(session.Turn, Nudge(session, applying), ct);
        var merged = reply is null ? null
            : guard.Merge(session.Proposal, reply.Proposal, Licences(applying), session.ParentNamed, session.LastRound);
        if (merged is not { Changed: true })
        {
            foreach (var draft in applying) Unchanged(session, draft);
            return false;
        }
        session.Proposal = merged.Merged;
        session.LastReply = reply!;
        session.Reverted = [.. ProposalSpecSet.Drafts(merged.Merged).Where(d => merged.Reverted.Contains(d.SpecId))];
        MasterOutcomes.PublishOutcome(session.Turn.Pipeline, merged.Merged, reply!.Result);
        foreach (var draft in applying) await SettleAsync(session, draft, ct);
        return true;
    }

    internal async Task<SpecRoundEvidenceResult> CheckAsync(ProposalReviewSession session, SpecDraft draft, CancellationToken ct)
    {
        try
        {
            return await evidence.CheckAsync(session.Turn.Pipeline, draft, session.Set, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The evidence of the design turn's draft {SpecId} could not be checked", draft.SpecId);
            return SpecRoundEvidenceResult.None;
        }
    }

    private async Task SettleAsync(ProposalReviewSession session, ProposalDraftReview draft, CancellationToken ct)
    {
        var now = ProposalSpecSet.Drafts(session.Proposal).First(d => d.SpecId == draft.SpecId);
        var changed = !string.Equals(now.Yaml, draft.Draft.Yaml, StringComparison.Ordinal);
        var (cuts, problems) = (draft.Merged!.Cuts, draft.Evidence.Problems);
        draft.Draft = now;
        if (changed) draft.Evidence = await CheckAsync(session, now, ct);
        IReadOnlyList<SpecReviewResidue> left = cuts.Count == 0 ? [] : residues.Of(now, cuts, problems);
        draft.Record(session.Round, left);
        draft.Stopped = !changed || (cuts.Count > 0 && SpecReviewStopRule.IsDone(draft.Merged, left, draft.Evidence));
    }

    private void Unchanged(ProposalReviewSession session, ProposalDraftReview draft)
    {
        draft.Record(session.Round, residues.Of(draft.Draft, draft.Merged!.Cuts, draft.Evidence.Problems));
        draft.Stopped = true;
    }

    private static string Nudge(ProposalReviewSession session, IReadOnlyList<ProposalDraftReview> applying) =>
        ProposalCutsNudge.For([.. applying.Select(d => new ProposalNudgeItem(
            d.Draft, d.Merged!.Cuts, d.Merged.Cuts.Count == 0 ? d.Merged.Findings : [], d.Evidence.Problems))],
            session.Reverted, session.LastRound);

    private static Dictionary<string, ProposalDraftLicence> Licences(IReadOnlyList<ProposalDraftReview> applying) =>
        applying.ToDictionary(d => d.SpecId, d => new ProposalDraftLicence(
            d.Merged!.Cuts, d.Evidence.Problems, d.Merged.Verdict == SpecReviewVerdicts.Refuse && d.Merged.Cuts.Count == 0),
            StringComparer.Ordinal);
}
