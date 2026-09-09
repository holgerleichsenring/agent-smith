using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: names the cause of the revision about to be written. The reviewer
/// WRITES: a foreign commit on the spec path is a correction, and calling it out
/// by name is what stops the next run from silently eating it — the whole point of
/// collecting it.
/// <para>
/// 2026-09-08-5cd2: the AUTHOR writes too. A ticket whose text no longer matches the
/// fingerprint the set was cut from is input the model has not seen; it outranks the
/// sha, because the branch set — reviewer edit included — is what the model amends,
/// and yields to a resume, which continues what the run was doing.
/// </para>
/// <para>
/// 2026-09-08-4aa9: so does a COMMENT — the objection the derivation-time comment invites.
/// It is read from the thread the run carries, the way the question pin and the repeat
/// guard read theirs: a comment by anyone but us after our last cut comment. It sits
/// where the edit sits, for the same reason. The executed-phase record moves the pointer
/// now, so the sha comparison below reads a marker's commit as this system's own.
/// </para>
/// </summary>
public static class SpecRevisionCause
{
    public const string Initial = "initial derivation";
    public const string ReviewerEdit = "reviewer edit on the ticket branch";
    public const string Resume = "resume";
    public const string Retrigger = "re-trigger on the ticket";
    public const string Comment = "comment on the ticket";
    public const string TicketEdit = "ticket text edited since the previous revision";

    /// <summary>
    /// A previous revision whose last commit is NOT the sha this system recorded was
    /// touched by someone else — that edit is the input, and the cause says so. An
    /// absent pointer reads as a foreign edit too: the safe direction, because the
    /// alternative is overwriting a correction we cannot rule out.
    /// </summary>
    public static string For(
        SpecSetReadResult? previous, SpecSetPointer? pointer, Ticket ticket, PipelineContext pipeline)
    {
        if (previous is null) return Initial;
        var resuming = pipeline.Has(ContextKeys.ResumeCheckpoint);
        if (!resuming && IsEdited(previous.Set, ticket)) return TicketEdit;
        if (!resuming && IsCommented(pipeline)) return Comment;
        if (pointer is null
            || !string.Equals(pointer.RevisionSha, previous.LastCommitSha, StringComparison.Ordinal))
            return ReviewerEdit;
        return resuming ? Resume : Retrigger;
    }

    // A set without a fingerprint predates it and compares as unchanged.
    private static bool IsEdited(SpecSet previous, Ticket ticket) =>
        previous.TicketFingerprint is { } cutFrom
        && !string.Equals(cutFrom, TicketTextFingerprint.Of(ticket), StringComparison.Ordinal);

    private static bool IsCommented(PipelineContext pipeline) =>
        OwnTicketComment.IsAnswered(
            pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var c) ? c : null,
            SpecSetComment.CutMarker);
}
