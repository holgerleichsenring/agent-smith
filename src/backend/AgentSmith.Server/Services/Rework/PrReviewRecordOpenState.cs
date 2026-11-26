using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-26-5201f: what a state read says only of an open pull request, mapped onto the record it builds — the draft
/// (2026-11-23-8d16), auto-complete (2026-11-24-629ab), the conflict and since when (2026-11-24-6811a) and whether a person's
/// approval is still required (2026-11-26-5201f; never of a draft). An ended pull request is none of them. Split out of
/// <see cref="PrReviewRecordRefresh"/>.
/// </summary>
public static class PrReviewRecordOpenState
{
    public static void Map(PrReviewState state, PrReviewRecord record)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(record);
        var open = state.State == PrReviewStates.Open;
        record.IsDraft = state.IsDraft && open;
        record.AutoCompleteArmed = open ? state.AutoCompleteArmed : null;
        record.HasConflicts = open ? state.HasConflicts : null;
        record.ConflictsAtTicks = record.HasConflicts == true ? record.CheckedAtTicks : null;
        // A draft cannot be completed: no approval is asked of anyone before it is published.
        record.ApprovalRequired = !open ? null : state.IsDraft && state.ApprovalRequired is not null ? false : state.ApprovalRequired;
    }
}
