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
/// <para>
/// 2026-09-22-8b25: and so does a DEMAND — a comment opening with the reserved phrase, which is
/// the one input that re-cuts a set somebody approved. It is decided FIRST, so a person who
/// demands a re-cut and edits the ticket in the same breath gets the re-cut rather than a notice
/// saying their edit was kept out; and it yields to a resume like every other input.
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

    /// <summary>2026-09-22-8b25: somebody wrote the reserved phrase in a comment and asked for the
    /// set to be cut again. It outranks the plain edit and comment causes because it is the one
    /// input that re-cuts a set a person APPROVED, and naming it is how the history can say why an
    /// approved set changed.</summary>
    public const string RecutDemand = "re-cut demanded on the ticket";

    /// <summary>2026-09-17-0e79a: the set was approved in the design conversation. The cause names
    /// the conversation it was approved in, so the revision history says which approval this
    /// revision is.</summary>
    public const string Approval = "approved in design conversation";

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
        if (!resuming && Demanded(previous.Set, pipeline) is not null) return RecutDemand;
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

    /// <summary>
    /// 2026-09-22-8b25: the demand this run must act on, or null. Public because the run has to
    /// know WHO demanded it and WHEN — a demand records a new approval, and the cause is only a
    /// string.
    /// </summary>
    public static TicketComment? Demanded(SpecSet? set, PipelineContext pipeline) =>
        SpecRecutDemand.From(pipeline, set);

    /// <summary>
    /// 2026-09-17-0e79b: whether anyone but us commented after our last cut comment — the same
    /// question <see cref="For"/> asks, made public because an EDIT outranks a comment here and
    /// would otherwise hide it. A run that keeps an approved set reports both, since neither was
    /// acted on and the cause can only name one.
    /// </summary>
    public static bool IsCommented(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return OwnTicketComment.IsAnswered(
            pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var c) ? c : null,
            SpecSetComment.CutMarker);
    }
}
