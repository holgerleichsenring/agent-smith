using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

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
    public const string NotReviewedWhy =
        "the review's share of this turn ran out before this spec was reviewed; it cannot be approved until a later turn reviews it";

    public static ProposalReviewOutcome Of(ProposalReviewSession session, IReadOnlyList<SpecEvidenceProblem> parentProblems)
    {
        var order = ProposalSpecSet.Drafts(session.Proposal).Select(d => d.SpecId).ToList();
        var drafts = session.Drafts.OrderBy(d => order.IndexOf(d.SpecId)).ToList();
        var waiting = drafts.Where(d => d.NotReviewed).Select(d => d.SpecId).ToList();
        var standing = Problems(ParentId(session.Proposal), parentProblems)
            .Concat(drafts.SelectMany(d => Problems(d.SpecId, d.Evidence.Problems).Concat(Uncleared(d))))
            .Concat(waiting.Select(id => new ProposalFinding(id, NotReviewedYet, NotReviewedWhy)))
            .ToList();
        return new ProposalReviewOutcome(session.Proposal, [.. drafts.SelectMany(d => d.Rounds)],
            waiting.Count == 0 ? null : waiting, standing, Reply(session));
    }

    private static IEnumerable<ProposalFinding> Problems(string? specId, IReadOnlyList<SpecEvidenceProblem> problems) =>
        specId is null ? [] : problems.Select(p => new ProposalFinding(specId, ProposalEvidenceReview.Problem, p.Problem, p.Claim));

    private static IEnumerable<ProposalFinding> Uncleared(ProposalDraftReview draft) =>
        draft.Rounds is [.., var last] && !Clears(last)
            ? last.Findings.Select(f => new ProposalFinding(draft.SpecId, last.Verdict, f.Text, null, f.Evidence))
            : [];

    private static bool Clears(ProposalReviewRound round) =>
        round.Verdict == SpecReviewVerdicts.Build
        || (round.Verdict == SpecReviewVerdicts.BuildWithCuts && round.Residues.Count == 0);

    private static string? ParentId(OutcomeProposal proposal) => (proposal as EpicOutcome)?.Parent.SpecId;

    private static Loop.AgenticLoopResult Reply(ProposalReviewSession session)
    {
        var published = ProposalSpecSet.Drafts(session.Proposal).Select(d => d.Yaml);
        if (ProposalSpecSet.Drafts(session.LastReply.Proposal).Select(d => d.Yaml).SequenceEqual(published, StringComparer.Ordinal))
            return session.LastReply.Result;
        var first = session.FirstReply.Response.Text ?? string.Empty;
        return MasterOutcomes.WithReplyText(session.FirstReply, ProposalReplyText.Render(first, session.Proposal));
    }
}
