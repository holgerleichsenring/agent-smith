using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-13-9a59b: a refreshed read copied onto the stored rows, column by column, so the change
/// tracker writes only what differs. A note that once carried a decision keeps it; whether the
/// decision still stands is <see cref="PrReviewEntry.Standing"/>.
/// </summary>
internal static class PrReviewRecordCopy
{
    public static void Record(PrReviewRecord read, PrReviewRecord stored)
    {
        var headMoved = read.HeadSha is { } head && stored.HeadSha is { } was && head != was; // 2026-11-24-297b
        AutoComplete(read, stored, stored.HasConflicts == true || headMoved); // 2026-11-24-629ab: before the conflict is overwritten
        Stamp(read, stored);
        stored.Url = read.Url;
        stored.Repo = read.Repo;
        stored.RepoKey = read.RepoKey;
        stored.Number = read.Number;
        stored.Project = read.Project;
        stored.TicketId = read.TicketId;
        stored.RunId = read.RunId;
        stored.State = read.State;
        stored.StateAtTicks = read.StateAtTicks;
        stored.HeadSha = read.HeadSha ?? stored.HeadSha;
        stored.IsDraft = read.IsDraft; // 2026-11-23-8d16
        Conflicts(read, stored, headMoved); // 2026-11-24-6811a
        stored.TargetBranch = read.TargetBranch ?? stored.TargetBranch; // 2026-11-22-6d85b: read once, after the merge
        stored.MergeSha = read.MergeSha ?? stored.MergeSha; // 2026-11-23-8df6a: a read that names none keeps the known one
        Ci(read, stored);
    }

    // 2026-11-17-3435a: a read without CI keeps the stored CI; the same state on the same head keeps when it was first seen
    // and the evidence read then (a later read of a red head skips the logs).
    private static void Ci(PrReviewRecord read, PrReviewRecord stored)
    {
        if (read.CiState is null) return;
        var same = read.CiState == stored.CiState && read.CiHeadSha == stored.CiHeadSha;
        stored.CiAtTicks = same ? stored.CiAtTicks ?? read.CiAtTicks : read.CiAtTicks;
        stored.CiFailuresJson = same ? stored.CiFailuresJson ?? read.CiFailuresJson : read.CiFailuresJson;
        stored.CiState = read.CiState;
        stored.CiHeadSha = read.CiHeadSha;
    }

    // 2026-11-24-6811a: an undecided read keeps what the host said last; the first conflicting read stamps since when; an
    // ended pull request conflicts with nothing. 2026-11-24-297b: a verdict belongs to its head — after a head move an
    // undecided read knows nothing (a refresh that pushed no longer shows the old head's conflict) and a conflicting
    // read begins a new conflict, which the conflict refresh is due once.
    // 2026-11-24-9dc0c: an undecided read makes a stored clean verdict unknown — the host no longer vouches for it (an Azure
    // merge computed against an older target) — while a stored conflict stays until the host decides again.
    private static void Conflicts(PrReviewRecord read, PrReviewRecord stored, bool headMoved)
    {
        if (PrReviewStates.IsEnded(read.State)) { stored.HasConflicts = null; stored.ConflictsAtTicks = null; return; }
        if (headMoved) { stored.HasConflicts = null; stored.ConflictsAtTicks = null; }
        if (read.HasConflicts is not { } conflicts)
        {
            if (stored.HasConflicts == false) stored.HasConflicts = null;
            return;
        }
        stored.ConflictsAtTicks = conflicts ? stored.ConflictsAtTicks ?? read.ConflictsAtTicks ?? read.CheckedAtTicks : null;
        stored.HasConflicts = conflicts;
    }

    // 2026-11-24-629ab: auto-complete that was set and is gone is recorded lost — dropped at a conflict — when the pull request
    // conflicts now or at the read before, or its head moved (a refresh that pushed before any read saw the conflict). Gone from
    // an unmoved head that does not conflict, even undecided, is a person's cancel and is never armed again. Set again or an
    // ended pull request clears the loss; a host that does not say keeps what was stored.
    private static void AutoComplete(PrReviewRecord read, PrReviewRecord stored, bool conflictedOrMoved)
    {
        if (PrReviewStates.IsEnded(read.State)) { stored.AutoCompleteArmed = null; stored.AutoCompleteLostAtTicks = null; return; }
        if (read.AutoCompleteArmed is not { } armed) return;
        if (armed) stored.AutoCompleteLostAtTicks = null;
        else if (stored.AutoCompleteArmed == true)
            stored.AutoCompleteLostAtTicks = read.HasConflicts == true || conflictedOrMoved ? read.CheckedAtTicks : null;
        stored.AutoCompleteArmed = armed;
    }

    // 2026-10-15-114fc: taken before the columns are overwritten; a read without a sha neither moves nor erases the head.
    private static void Stamp(PrReviewRecord read, PrReviewRecord stored)
    {
        if (read.HeadSha is { } head && stored.HeadSha is { } was && head != was) stored.HeadMovedAtTicks = read.CheckedAtTicks;
        if (PrReviewStates.IsEnded(stored.State) && read.State == PrReviewStates.Open)
            stored.ReopenedAtTicks = read.CheckedAtTicks;
    }

    public static void Entry(PrReviewEntry read, PrReviewEntry stored)
    {
        stored.Kind = read.Kind;
        stored.Decision = read.Decision ?? stored.Decision;
        stored.Standing = read.Standing;
        stored.AuthorLogin = read.AuthorLogin;
        stored.Trusted = read.Trusted;
        stored.IsOurs = read.IsOurs;
        stored.IsBot = read.IsBot;
        stored.IsSystem = read.IsSystem;
        stored.IsVote = read.IsVote;
        stored.File = read.File;
        stored.Line = read.Line;
        stored.Resolved = read.Resolved;
        stored.ThreadHostId = read.ThreadHostId;
        stored.Body = read.Body;
        stored.Truncated = read.Truncated;
        stored.AtTicks = read.AtTicks;
    }
}
