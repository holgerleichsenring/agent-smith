using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: names the cause of the revision about to be written. The reviewer
/// WRITES: a foreign commit on the spec path is a correction, and calling it out
/// by name is what stops the next run from silently eating it — the whole point of
/// collecting it.
/// </summary>
public static class SpecRevisionCause
{
    public const string Initial = "initial derivation";
    public const string ReviewerEdit = "reviewer edit on the ticket branch";
    public const string Resume = "resume";
    public const string Retrigger = "re-trigger on the ticket";
    public const string Comment = "comment on the ticket";
    public const string Recut = "re-cut of the unexecuted tail";

    /// <summary>
    /// A previous revision whose last commit is NOT the sha this system recorded was
    /// touched by someone else — that edit is the input, and the cause says so. An
    /// absent pointer reads as a foreign edit too: the safe direction, because the
    /// alternative is overwriting a correction we cannot rule out.
    /// </summary>
    public static string For(
        SpecSetReadResult? previous, SpecSetPointer? pointer, PipelineContext pipeline)
    {
        if (previous is null) return Initial;
        if (pointer is null
            || !string.Equals(pointer.RevisionSha, previous.LastCommitSha, StringComparison.Ordinal))
            return ReviewerEdit;
        return pipeline.Has(ContextKeys.ResumeCheckpoint) ? Resume : Retrigger;
    }
}
