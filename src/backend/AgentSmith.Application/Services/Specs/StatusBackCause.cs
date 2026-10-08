using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-08-2123: a person moved a finished ticket back — the run read the move from the tracker's
/// history — and there is something to act on: a comment not ours, a review note or a changed ticket
/// text since the previous attempt's cutoff. It names the revision only when the set is fully
/// executed, the one shape in which the move has to append; a set with an unexecuted tail keeps the
/// existing causes. The comment mappers carry no bot flag and GitHub and GitLab no timestamped edit,
/// so the fingerprint stands for the edit.
/// </summary>
public static class StatusBackCause
{
    public static bool Applies(SpecSet set, Ticket ticket, PipelineContext pipeline)
    {
        if (!pipeline.Has(ContextKeys.StatusBackAct)) return false;
        if (set.UnexecutedTail.Count != 0 || set.Executed.Count == 0) return false;
        if (!pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var attempt) || attempt is null) return false;
        return CommentedSince(pipeline, attempt) || ReviewedSince(pipeline, attempt) || Edited(set, ticket);
    }

    private static bool CommentedSince(PipelineContext pipeline, PreviousAttempt attempt) =>
        pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var comments)
        && comments is not null && comments.Any(c => !OwnTicketComment.IsOurs(c) && attempt.Precedes(c.CreatedAt));

    private static bool ReviewedSince(PipelineContext pipeline, PreviousAttempt attempt) =>
        pipeline.TryGet<IReadOnlyList<PrReviewFeedback>>(ContextKeys.PrReviewFeedback, out var feedback)
        && feedback is not null && feedback.SelectMany(f => f.Threads).SelectMany(t => t.Notes).Any(n => attempt.Precedes(n.At));

    private static bool Edited(SpecSet set, Ticket ticket) =>
        set.TicketFingerprint is { } cutFrom
        && !string.Equals(cutFrom, TicketTextFingerprint.Of(ticket), StringComparison.Ordinal);
}
