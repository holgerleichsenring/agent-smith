using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762h: how a branch set carrying a review-objection question is read back. The question is about
/// a set we keep, so its hand-back is ALWAYS stripped: kept, it would hide the previous cut from the deriver
/// and, on a set used unchanged, be republished and park again. A bare re-trigger over an objection we asked and
/// nobody answered keeps the set unchanged; 2026-11-10-8e59: its specs carry no clean review, so the run attacks them
/// again from fresh rounds, and nothing is built on silence. Every other
/// cause, and every other set, is the amendment rule's, so an answer by comment or ticket edit is derived over.
/// </summary>
public static class ReviewObjectionSource
{
    public static (SpecSet Set, bool NeedsModel) Decide(
        SpecSet set, string cause, PipelineContext pipeline, bool foreign, SeriesConfig series)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(pipeline);
        if (set.Handback is not { Case: SpecHandbackCase.Question, QuestionKind: SpecQuestionKind.ReviewObjection })
            return (set, SpecAmendmentRule.NeedsModel(cause, set, series, foreign));
        var stripped = set with { Handback = null };
        var proceed = string.Equals(cause, SpecRevisionCause.Retrigger, StringComparison.Ordinal)
            && ReviewObjectionComment.AskedAndUnanswered(pipeline);
        return (stripped, !proceed && SpecAmendmentRule.NeedsModel(cause, stripped, series, foreign));
    }
}
