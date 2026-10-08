using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9b: the rework act a run serves, read by the run itself from the thread it
/// fetched — so the act reaches the run whichever claimer won the lease. It is the newest comment
/// not ours that carries the trigger's comment_keyword and was written clearly after the previous
/// attempt started (the skew margin of <see cref="PreviousAttempt"/>). No previous attempt, no
/// keyword or no such comment: no act.
/// </summary>
public static class ReworkActReader
{
    public static ReworkAct? Read(
        IReadOnlyList<TicketComment>? comments, string? keyword, PreviousAttempt? attempt)
    {
        if (attempt is null || string.IsNullOrWhiteSpace(keyword) || comments is null) return null;
        var act = comments
            .Where(c => !OwnTicketComment.IsOurs(c) && attempt.Precedes(c.CreatedAt)
                && c.Body?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true)
            .MaxBy(c => c.CreatedAt);
        return act is null ? null : new ReworkAct(act.Author, act.CreatedAt);
    }

    /// <summary>Reads the act off the pipeline and sets <see cref="ContextKeys.ReworkAct"/>.</summary>
    public static void Apply(PipelineContext pipeline, string? keyword)
    {
        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var c) ? c : null;
        var attempt = pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var a) ? a : null;
        if (Read(comments, keyword, attempt) is { } act) pipeline.Set(ContextKeys.ReworkAct, act);
    }
}
