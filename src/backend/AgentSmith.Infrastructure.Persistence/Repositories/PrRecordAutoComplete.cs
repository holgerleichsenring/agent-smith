using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-24-629ab: the auto-complete part of a refreshed read's copy — whether auto-complete that was set and is gone was lost
/// to the host (and is armed again) or taken off by a person. 2026-11-25-098f: split out of <see cref="PrReviewRecordCopy"/>.
/// </summary>
internal static class PrRecordAutoComplete
{
    /// <summary>
    /// 2026-11-25-098f: the head whose pusher decides this read — the head auto-complete was found gone on and that no run had
    /// recorded yet, else the read's head when auto-complete that was set is gone on a moved head; null when no pusher decides.
    /// The record writer asks whether agent-smith pushed it and passes the answer to <see cref="PrReviewRecordCopy.Record"/>.
    /// </summary>
    public static string? HeadInQuestion(PrReviewRecord read, PrReviewRecord stored)
    {
        if (PrReviewStates.IsEnded(read.State) || read.AutoCompleteArmed != false) return null;
        if (stored.AutoCompleteGoneHead is { } gone) return gone;
        return stored.AutoCompleteArmed == true && HeadMoved(read, stored) ? read.HeadSha : null;
    }

    public static bool HeadMoved(PrReviewRecord read, PrReviewRecord stored) =>
        read.HeadSha is { } head && stored.HeadSha is { } was && head != was;

    // 2026-11-24-629ab: auto-complete that was set and is gone is recorded lost when the host dropped it: on an unmoved head that
    // conflicts now or at the read before (Azure's drop at a conflict). Gone from an unmoved head that does not conflict, even
    // undecided, is a person's cancel and is never armed again. 2026-11-25-098f: on a moved head (GitLab cancels auto-merge on
    // every new commit) it is lost only when agent-smith pushed that head — a refresh, a rework, a CI fix; a person's push, even
    // to a conflicting pull request, leaves it off. The run's record of its push may land after the read (the event stream), so
    // a head no run recorded yet is kept (AutoCompleteGoneHead) and asked about again at every read until a run records it;
    // a later push on top changes nothing about who pushed that head. Set again or an ended pull request clears the loss and
    // the head in question; a host that does not say keeps what was stored.
    public static void Copy(PrReviewRecord read, PrReviewRecord stored, bool headMoved, bool ownHead)
    {
        if (PrReviewStates.IsEnded(read.State))
        {
            (stored.AutoCompleteArmed, stored.AutoCompleteLostAtTicks, stored.AutoCompleteGoneHead) = (null, null, null);
            return;
        }
        if (read.AutoCompleteArmed is not { } armed) return;
        if (armed) (stored.AutoCompleteLostAtTicks, stored.AutoCompleteGoneHead) = (null, null);
        else if (stored.AutoCompleteGoneHead is not null)
        {
            if (ownHead) (stored.AutoCompleteLostAtTicks, stored.AutoCompleteGoneHead) = (read.CheckedAtTicks, null);
        }
        else if (stored.AutoCompleteArmed == true && headMoved)
        {
            if (ownHead) stored.AutoCompleteLostAtTicks = read.CheckedAtTicks;
            else stored.AutoCompleteGoneHead = read.HeadSha;
        }
        else if (stored.AutoCompleteArmed == true)
            stored.AutoCompleteLostAtTicks = read.HasConflicts == true || stored.HasConflicts == true ? read.CheckedAtTicks : null;
        stored.AutoCompleteArmed = armed;
    }
}
