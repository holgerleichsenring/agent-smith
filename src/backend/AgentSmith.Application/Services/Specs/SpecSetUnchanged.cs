using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-17-7c9ba: whether a run's next revision would say nothing the branch does not already say.
/// A plain re-trigger that ran no model, pinned no question, had nothing discarded and is not a preview
/// or a hand-back carries no input; when the set it would publish equals the one it read, a revision
/// would be a commit and a ticket comment about nothing. Resume is left out on purpose: a run that died
/// between its revision and its cut comment needs the resume to post the comment that anchors the thread.
/// <para>
/// 2026-11-28-db88a: the two are compared as STORED — the whole set, its revisions aside — not as rendered files: with no
/// manifest the files carry no series-wide field, and a stand-in, contract, goal, approval, fingerprint or accounting
/// change would read as unchanged and never be stored.
/// </para>
/// </summary>
public sealed class SpecSetUnchanged
{
    public bool Holds(
        SpecSet finalized, SpecSetReadResult? previous, SpecSourceResolver.Decision decision,
        bool questionPinned, bool preview)
    {
        ArgumentNullException.ThrowIfNull(finalized);
        ArgumentNullException.ThrowIfNull(decision);
        if (previous is null || decision.NeedsModel || questionPinned || preview || finalized.IsHandedBack) return false;
        if (decision.Cause != SpecRevisionCause.Retrigger || decision.Discarded is not null) return false;
        if (string.IsNullOrWhiteSpace(finalized.Series)) return false;
        var read = previous.Set with { Series = finalized.Series };
        return string.Equals(
            SpecSetJson.Write(finalized with { Revisions = read.Revisions }), SpecSetJson.Write(read), StringComparison.Ordinal);
    }
}
