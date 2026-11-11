using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: a review round's changes applied by the design partner. The drafts with cuts or a REFUSE
/// still admitted by <see cref="ProposalReviewSession.Admit"/> go into ONE re-prompt (<see cref="ProposalCutsNudge"/>);
/// the re-proposal is merged by <see cref="ProposalReProposalGuard"/> and published; each draft gets its
/// residues, its re-checked evidence and its round, and stops on the stop rule or on unchanged text. A failed
/// call, an unusable reply or one that changed no draft ends the review on the last valid proposal; 2026-11-11-2bdfb:
/// a reply not used leaves its reason on each draft it was for. A re-cut adds and drops children's reviews.
/// </summary>
public sealed class ProposalRoundRevision(
    ProposalCutsReprompt reprompt, ProposalReProposalGuard guard, SpecCutResidues residues, ISpecRoundEvidence evidence,
    ILogger<ProposalRoundRevision> logger)
{
    /// <summary>False when the review ends here.</summary>
    internal async Task<bool> ApplyAsync(
        ProposalReviewSession session, IReadOnlyList<ProposalDraftReview> toFix, CancellationToken ct)
    {
        var applying = session.Admit(toFix);
        foreach (var draft in toFix.Except(applying)) Unchanged(session, draft);
        if (applying.Count == 0) return true;
        var asked = ProposalOpenQuestions.Of(session.Turn.Pipeline); // 2026-11-11-2bdfc: the questions before the re-prompt
        var reply = await reprompt.RunAsync(session.Turn, Nudge(session, applying), ct);
        var merged = reply is null ? null
            : guard.Merge(session.Proposal, reply.Proposal, Licences(applying), session.ParentNamed, session.LastRound);
        if (merged is not { Void: null, Changed: true })
        {
            // 2026-11-11-2bdfb: a reply not used says why on every draft it was for.
            foreach (var draft in applying) Unchanged(session, draft, merged is null ? ProposalReviewSummary.UnresolvedWhy : merged.Void);
            if (merged is not { Void: null }) ProposalOpenQuestions.Restore(session.Turn.Pipeline, asked);
            return false;
        }
        if (!Admitted(session.Proposal, merged.Merged) || ProposalOpenQuestions.Of(session.Turn.Pipeline).Count == 0)
            ProposalOpenQuestions.Restore(session.Turn.Pipeline, asked);
        session.Proposal = merged.Merged;
        session.LastReply = reply!;
        var stated = ProposalSpecSet.Drafts(merged.Merged);
        session.Reverted = [.. stated.Where(d => merged.Reverted.Contains(d.SpecId))];
        session.LeftOut = [.. merged.Reverted.Where(id => stated.All(d => d.SpecId != id))];
        MasterOutcomes.PublishOutcome(session.Turn.Pipeline, merged.Merged, reply!.Result);
        await RecutAsync(session, merged, ct);
        foreach (var draft in applying.Where(session.Drafts.Contains)) await SettleAsync(session, draft, ct);
        return !WaitsOnAnswers(session, asked);
    }

    // 2026-11-11-2bdfe: a revision that lays new questions on the operator ends the review; what is open waits on the answers.
    private static bool WaitsOnAnswers(ProposalReviewSession session, IReadOnlyList<OpenQuestion> asked)
    {
        if (!ProposalOpenQuestions.AddsTo(session.Turn.Pipeline, asked)) return false;
        var why = ProposalReviewSummary.WaitingOnAnswersWhy(ProposalOpenQuestions.Of(session.Turn.Pipeline).Count);
        foreach (var draft in session.Open) draft.StoppedBecause = why;
        return true;
    }

    // 2026-11-11-2bdfb: a dropped child's review leaves the session; an added one enters it open, its evidence checked.
    private async Task RecutAsync(ProposalReviewSession session, ProposalGuardResult merged, CancellationToken ct)
    {
        session.Drafts.RemoveAll(d => merged.Dropped.Contains(d.SpecId, StringComparer.Ordinal));
        foreach (var draft in ProposalSpecSet.Drafts(merged.Merged).Where(d => merged.Added.Contains(d.SpecId)))
            session.Drafts.Add(new ProposalDraftReview(draft) { Evidence = await CheckAsync(session, draft, ct) });
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

    private void Unchanged(ProposalReviewSession session, ProposalDraftReview draft, string? notUsed = null)
    {
        draft.Record(session.Round, residues.Of(draft.Draft, draft.Merged!.Cuts, draft.Evidence.Problems));
        draft.Stopped = true;
        draft.NotUsedBecause = notUsed;
    }

    // 2026-11-11-2bdfc: a used reply all of whose changes were reverted leaves the drafts the earlier questions came with.
    private static bool Admitted(OutcomeProposal before, OutcomeProposal merged) =>
        !ProposalSpecSet.Drafts(before).Select(d => d.Yaml).SequenceEqual(ProposalSpecSet.Drafts(merged).Select(d => d.Yaml), StringComparer.Ordinal);

    private static string Nudge(ProposalReviewSession session, IReadOnlyList<ProposalDraftReview> applying) =>
        ProposalCutsNudge.For([.. applying.Select(d => new ProposalNudgeItem(
            d.Draft, d.Merged!.Cuts, d.Merged.Cuts.Count == 0 ? d.Merged.Findings : [], d.Evidence.Problems))],
            session.Reverted, session.LastRound, session.LeftOut, session.Proposal is EpicOutcome);

    private static Dictionary<string, ProposalDraftLicence> Licences(IReadOnlyList<ProposalDraftReview> applying) =>
        applying.ToDictionary(d => d.SpecId, d => new ProposalDraftLicence(
            d.Merged!.Cuts, d.Evidence.Problems, d.Merged.Verdict == SpecReviewVerdicts.Refuse && d.Merged.Cuts.Count == 0),
            StringComparer.Ordinal);
}
