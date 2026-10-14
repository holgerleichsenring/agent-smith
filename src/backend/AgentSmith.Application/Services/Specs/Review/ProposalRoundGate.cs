using System.Globalization;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: which open drafts a design turn may still review, asked before every round and every
/// re-prompt. The run cap, or a wall clock the next round would carry past the ceiling (time since the run
/// started plus the previous round's duration), stops every open draft. A spent review share stops all but the
/// floor — the turn's first open draft, which is reviewed to its stop so every turn makes progress. A draft
/// stopped here is not reviewed.
/// </summary>
internal static class ProposalRoundGate
{
    public static List<ProposalDraftReview> Admit(ProposalReviewSession session, IReadOnlyList<ProposalDraftReview> open)
    {
        if (open.Count == 0) return [];
        session.Floor ??= open[0];
        var pipeline = session.Turn.Pipeline;
        var hard = SpecReviewBudget.Exhausted(pipeline, session.Settings) ?? Ceiling(session);
        if ((hard ?? SpecReviewBudget.Spent(pipeline, session.Settings)) is null) return [.. open];
        var kept = hard is null ? open.Where(draft => draft == session.Floor).ToList() : [];
        foreach (var draft in open.Except(kept))
        {
            draft.Stopped = true;
            draft.NotReviewed = true;
        }
        return kept;
    }

    private static string? Ceiling(ProposalReviewSession session)
    {
        if (!session.Turn.Pipeline.TryGet<DateTimeOffset>(ContextKeys.RunStartedAt, out var started)) return null;
        var reach = (session.Clock.GetUtcNow() - started + session.LastRoundDuration).TotalSeconds;
        return reach >= session.Settings.MaxRunWallTimeSeconds
            ? string.Create(CultureInfo.InvariantCulture, $"another round would pass the wall-time ceiling ({reach:0} s)")
            : null;
    }
}
