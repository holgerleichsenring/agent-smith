using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: a design turn's review as it is left on the proposal. Rounds in draft order; what still
/// stands — every evidence problem, the last round's findings of a draft its rounds did not clear, and one
/// "not reviewed yet" finding per spec id not reviewed, so a chat confirmation lists it too. The reply is the
/// last one when it proposed exactly what is published, otherwise the first reply's prose around the published
/// drafts (<see cref="ProposalReplyText"/>).
/// </summary>
internal static class ProposalReviewSummary
{
    public const string NotReviewedYet = "not reviewed yet";
    /// <summary>2026-11-10-18cf: the finding names why the review did not finish. 2026-11-10-8e59: information, not
    /// a hold — the spec is reviewed in its exact text before it is built.</summary>
    public static string NotReviewedWhy(string reason) =>
        $"not yet reviewed ({reason}); it will be reviewed before it is built";

    /// <summary>2026-11-10-18cf: <paramref name="proposal"/> with every one of <paramref name="drafts"/> marked not
    /// reviewed for <paramref name="reason"/>, beside <paramref name="findings"/>.</summary>
    public static OutcomeProposal MarkedNotReviewed(
        OutcomeProposal proposal, IReadOnlyList<SpecDraft> drafts, IReadOnlyList<ProposalFinding> findings, string reason) =>
        proposal with
        {
            NotReviewed = drafts.Count == 0 ? null : [.. drafts.Select(d => d.SpecId)],
            Findings = [.. findings, .. drafts.Select(d => new ProposalFinding(d.SpecId, NotReviewedYet, NotReviewedWhy(reason)))],
        };

    public const string NoAnswerWhy = "no reviewer answered (each call failed, timed out or could not be read)";
    public const string LastRevisionWhy = "its last revision came after the final round, so nobody attacked that text";
    public const string UnderReviewWhy = "it was still under review when this was published, and a turn that ends here leaves it unreviewed";

    public const string UnreadWhy = "the reviewers could not read what this spec names";

    /// <summary>2026-11-11-2bdfe: a revision laid new questions on the operator, so the review waits on the answers.</summary>
    public static string WaitingOnAnswersWhy(int questions) =>
        $"it rests on the operator's {questions} open question(s) and is reviewed again once they are answered";

    /// <summary>2026-11-11-2bdfb: the standing finding on a draft whose re-prompt's reply was not used.</summary>
    public const string NotUsedProblem = "revision not used";
    public const string UnresolvedWhy = "the re-prompt failed or its reply did not resolve to a proposal";
    public static string NotUsedWhy(string reason) => $"the design partner's revision of this spec was not used: {reason}";

    /// <summary>
    /// The review as it stands. A draft still open is not reviewed — its current text was never attacked — and
    /// says why: what stopped it, or <paramref name="final"/> ? its last revision came too late : still under review.
    /// </summary>
    public static ProposalReviewOutcome Of(
        ProposalReviewSession session, IReadOnlyList<SpecEvidenceProblem> parentProblems, bool final)
    {
        var order = ProposalSpecSet.Drafts(session.Proposal).Select(d => d.SpecId).ToList();
        var drafts = session.Drafts.OrderBy(d => order.IndexOf(d.SpecId)).ToList();
        var unfinished = drafts.Where(d => d.NotReviewed || !d.Stopped).ToList();
        var waiting = unfinished.Select(d => d.SpecId).ToList();
        var standing = Problems(ParentId(session.Proposal), parentProblems)
            .Concat(drafts.SelectMany(d => Problems(d.SpecId, d.Evidence.Problems).Concat(Uncleared(d))))
            .Concat(drafts.Where(d => d.NotUsedBecause is not null)
                .Select(d => new ProposalFinding(d.SpecId, NotUsedProblem, NotUsedWhy(d.NotUsedBecause!))))
            .Concat(unfinished.Select(d => new ProposalFinding(d.SpecId, NotReviewedYet,
                NotReviewedWhy(d.StoppedBecause ?? (final ? LastRevisionWhy : UnderReviewWhy)))))
            .ToList();
        return new ProposalReviewOutcome(session.Proposal, [.. drafts.SelectMany(d => d.Rounds)],
            waiting.Count == 0 ? null : waiting, standing, Reply(session))
        {
            ReviewedClean = [.. drafts.Where(d => d.ReviewedClean).Select(d => ReviewedText.Of(d.SpecId, d.Draft.Yaml))],
        };
    }

    private static IEnumerable<ProposalFinding> Problems(string? specId, IReadOnlyList<SpecEvidenceProblem> problems) =>
        specId is null ? [] : problems.Select(p => new ProposalFinding(specId, ProposalEvidenceReview.Problem, p.Problem, p.Claim));

    // 2026-11-10-18cf: a draft still open after a rewrite shows no findings about the text it replaced.
    private static IEnumerable<ProposalFinding> Uncleared(ProposalDraftReview draft) =>
        draft.Rounds is [.., var last] && !Clears(last) && (draft.Stopped || draft.AttackedCurrentText)
            ? last.Findings.Select(f => new ProposalFinding(draft.SpecId, last.Verdict, f.Text, null, f.Evidence))
            : [];

    private static bool Clears(ProposalReviewRound round) =>
        round.Verdict == SpecReviewVerdicts.Build
        || (round.Verdict == SpecReviewVerdicts.BuildWithCuts && round.Residues.Count == 0);

    private static string? ParentId(OutcomeProposal proposal) => (proposal as EpicOutcome)?.Parent.SpecId;

    // 2026-11-09-0620m: the operator reads the turn's first full reply — its analysis, contradictions and questions —
    // with its block replaced by the published one. A re-draft's own prose is a note to the reviewers, often one line
    // in another language, and replaced all of that when the published draft was the re-draft's.
    private static Loop.AgenticLoopResult Reply(ProposalReviewSession session)
    {
        if (ReferenceEquals(session.LastReply.Result, session.FirstReply)) return session.FirstReply;
        var first = session.FirstReply.Response.Text ?? string.Empty;
        return MasterOutcomes.WithReplyText(session.FirstReply,
            ProposalReplyText.Render(first, session.Proposal, session.LastReply.Result.Response.Text));
    }
}
