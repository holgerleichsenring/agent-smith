using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: a design turn's proposal reviewed in rounds before a person can approve it — over a lone
/// spec or an epic's CHILDREN (the parent, never built, gets one evidence check). Each round every open draft is
/// checked and attacked by spec_review.reviewers fresh reviewers, merged, and either stops (BUILD, nothing to
/// apply, nothing taken) or goes into the round's ONE re-prompt of the design partner
/// (<see cref="ProposalRoundRevision"/>). Drafts carried over from the proposal under revision count as
/// stopped; <see cref="ProposalRoundGate"/> decides what the share and the ceiling still allow. 8762k: an epic with a
/// contract then gets its findings-only series round (<see cref="ProposalSeriesRound"/>).
/// </summary>
public sealed class ProposalReviewLoop(
    ProposalAttackPanel panel, ProposalRoundRevision revision, ProposalSeriesRound series,
    ITurnActivityObserverAccessor turnActivity, TimeProvider clock)
{
    public async Task<ProposalReviewOutcome> RunAsync(
        ProposalTurn turn, SpecReviewSettings settings, OutcomeProposal proposal, AgenticLoopResult reply,
        IReadOnlyList<ProposalFinding> wholeSet, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(wholeSet);
        var parent = (proposal as EpicOutcome)?.Parent;
        var session = new ProposalReviewSession(turn, settings, proposal, reply,
            ProposalReviewCarryOver.Seed(turn.Pipeline, Reviewed(proposal)), clock)
        {
            ParentNamed = parent is not null && wholeSet.Any(f => f.SpecId == parent.SpecId),
        };
        foreach (var draft in session.Drafts) draft.Evidence = await revision.CheckAsync(session, draft.Draft, ct);
        var parentProblems = parent is null ? [] : (await revision.CheckAsync(session, parent, ct)).Problems;
        while (session.Round < settings.Review.Rounds
            && ProposalRoundGate.Admit(session, session.Open) is { Count: > 0 } open)
            if (!await RoundAsync(session, open, ct)) break;
        return await series.AddAsync(session, ProposalReviewSummary.Of(session, parentProblems), ct);
    }

    /// <summary>False when the review ends after this round.</summary>
    private async Task<bool> RoundAsync(ProposalReviewSession session, List<ProposalDraftReview> open, CancellationToken ct)
    {
        var started = clock.GetUtcNow();
        session.Round++;
        await turnActivity.ReportAsync(new TurnActivity(TurnActivityKind.Reviewing), ct);
        var answers = await panel.ReviewAsync(
            session.Turn, [.. open.Select(draft => Input(session, draft))], session.Settings.Review, ct);
        var toFix = open.Where((draft, i) => Judge(session, draft, answers[i])).ToList();
        var goesOn = toFix.Count == 0 || await revision.ApplyAsync(session, toFix, ct);
        session.LastRoundDuration = clock.GetUtcNow() - started;
        return goesOn;
    }

    /// <summary>True when the draft goes into the re-prompt; otherwise it stops here.</summary>
    private static bool Judge(ProposalReviewSession session, ProposalDraftReview draft, IReadOnlyList<SpecAttackResult?> answers)
    {
        var merged = SpecReviewMerge.Merge(answers);
        if (merged.IsTaken)
        {
            draft.Merged = merged;
            draft.Verdicts = [.. answers.Select(answer => answer?.Verdict)];
        }
        var toFix = merged.Cuts.Count > 0 || merged.Verdict == SpecReviewVerdicts.Refuse;
        if (merged.IsTaken && !toFix) draft.Record(session.Round, []);
        draft.Stopped = !toFix;
        return toFix;
    }

    private static SpecAttackInput Input(ProposalReviewSession session, ProposalDraftReview draft) =>
        new(draft.Draft, SpecReviewScopes.Spec, session.Round, 0, (session.Proposal as EpicOutcome)?.Parent.Goal,
            SpecToday.Describes(draft.Draft, session.Set), draft.Evidence.Problems, draft.Evidence.Unchecked,
            draft.Evidence.Total, draft.Residues)
        {
            RepositoryNames = SpecReviewRepositories.Names(session.Turn.Pipeline),
        };

    private static IReadOnlyList<SpecDraft> Reviewed(OutcomeProposal proposal) => proposal switch
    {
        SpecOutcome spec => [spec.Draft],
        EpicOutcome epic => epic.Children,
        _ => [],
    };
}
