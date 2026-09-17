using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79b: what the cut comment tells the author about changing the set — which is not
/// the same sentence for a set a person approved.
/// <para>
/// The comment used to promise, on every run, that "the next run amends an unstarted phase or
/// re-cuts the unstarted tail". For an approved set that promise is FALSE and contradicts the
/// notice the run posts beside it, so it branches on the approval mark and points at the place a
/// change is actually made. What does not branch is <see cref="SpecSetComment.CutMarker"/> in the
/// heading: it is the anchor the comment rule measures from and it stays in both wordings.
/// </para>
/// <para>
/// WHERE IT POINTS IS WHAT WORKS TODAY: the specs on the ticket branch, open in the draft pull
/// request. A reviewer's edit there is read back by the next run and worked as it stands —
/// unchanged by this phase, and the only route an operator can take right now. It deliberately
/// does NOT send them to the design conversation: nothing re-opens an approved record from one
/// yet (the store side exists, the dialog command does not), and pointing at an unreachable
/// place would be the promise this phase deleted, moved somewhere else.
/// </para>
/// </summary>
public static class SpecSetCommentPreamble
{
    public static string For(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return set.Approval is null ? Derived : Approved;
    }

    private const string Derived =
        "I split it into the phases below and started working. This is NOT a question and "
        + "the run is not waiting: comment if the cut is wrong and the next run amends an "
        + "unstarted phase or re-cuts the unstarted tail. A phase that already ran is never "
        + "edited — a correction to it becomes a new phase.";

    private const string Approved =
        "This set was approved in a design conversation and I started working. This is NOT a "
        + "question and the run is not waiting — and a comment here will NOT change the "
        + "specification: no run re-cuts a set somebody approved. To change it, edit the phase "
        + "specs on the ticket branch (they are open in the pull request linked below) and "
        + "re-trigger: the next run works them as they stand. A phase that already ran is never "
        + "edited — a correction to it becomes a new phase. Comment anyway if something is wrong; "
        + "the run records it and says so, it just will not act on it.";
}
