using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-12-cd4ch: ONE advisory review pass over a design turn's proposal — a lone spec or an epic's CHILDREN
/// (the parent, never built, gets its evidence check). Every draft's evidence is checked, every open draft is
/// attacked once by spec_review.reviewers fresh reviewers beside its siblings and the operator's open questions,
/// and an epic with a contract gets its series round. Nothing is re-prompted: what the reviewers say is advice on
/// the card. Drafts carried over unchanged from the proposal under revision keep their review; only the run's
/// cost cap stops the pass (<see cref="ProposalReviewSession.Admit"/>).
/// </summary>
public sealed class ProposalAttackPass(
    ProposalAttackPanel panel, ISpecRoundEvidence evidence, ProposalSeriesRound series, ILogger<ProposalAttackPass> logger)
{
    public async Task<ProposalReviewOutcome> RunAsync(
        ProposalTurn turn, SpecReviewSettings settings, OutcomeProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(proposal);
        var session = new ProposalReviewSession(turn, settings, proposal,
            ProposalReviewCarryOver.Seed(turn.Pipeline, Reviewed(proposal)));
        foreach (var draft in session.Drafts) draft.Evidence = await CheckAsync(session, draft.Draft, ct);
        var parent = (proposal as EpicOutcome)?.Parent;
        var parentProblems = parent is null ? [] : (await CheckAsync(session, parent, ct)).Problems;
        if (session.Admit(session.Open) is { Count: > 0 } open)
        {
            var answers = await panel.ReviewAsync(turn, [.. open.Select(draft => Input(session, draft))], settings.Review, ct);
            for (var i = 0; i < open.Count; i++) Judge(session, open[i], answers[i]);
        }
        return await series.AddAsync(session, ProposalReviewSummary.Of(session, parentProblems), ct);
    }

    private static void Judge(ProposalReviewSession session, ProposalDraftReview draft, IReadOnlyList<SpecAttackResult?> answers)
    {
        var merged = SpecReviewMerge.Merge(answers);
        if (merged.Blind) draft.MarkUnread();
        // 2026-11-10-18cf: with repositories to read and no reviewer answering, nothing attacked this text (85e2).
        else if (!merged.IsTaken && CouldRead(session)) draft.MarkNotReviewed(ProposalReviewSummary.NoAnswerWhy);
        else if (!merged.IsTaken) draft.Stopped = true;
        else
        {
            draft.AttackedYaml = draft.Draft.Yaml;
            draft.Merged = merged;
            draft.Verdicts = [.. answers.Select(answer => answer is { Blind: false } ? answer.Verdict : null)];
            draft.Record();
            draft.Stopped = true;
        }
    }

    private static bool CouldRead(ProposalReviewSession session) =>
        ReviewReferences.Beside(session.Turn.Pipeline, DerivationLookFactory.ProposalRepositories(session.Turn.Pipeline)) is not null;

    private async Task<SpecRoundEvidenceResult> CheckAsync(ProposalReviewSession session, SpecDraft draft, CancellationToken ct)
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

    private static SpecAttackInput Input(ProposalReviewSession session, ProposalDraftReview draft) =>
        new(draft.Draft, SpecReviewScopes.Spec, 1, 0, (session.Proposal as EpicOutcome)?.Parent.Goal,
            SpecToday.Describes(draft.Draft, session.Set), draft.Evidence.Problems, draft.Evidence.Unchecked,
            draft.Evidence.Total, [])
        {
            RepositoryNames = SpecReviewRepositories.Names(session.Turn.Pipeline), Proposal = true, // 14a3g
            Siblings = ProposalAttackSiblings.Of(session, draft.Draft), // 2026-11-11-2bdfa
            OpenQuestions = ProposalOpenQuestions.Of(session.Turn.Pipeline), // 2026-11-11-2bdfd
        };

    private static IReadOnlyList<SpecDraft> Reviewed(OutcomeProposal proposal) => proposal switch
    {
        SpecOutcome spec => [spec.Draft],
        EpicOutcome epic => epic.Children,
        _ => [],
    };
}
