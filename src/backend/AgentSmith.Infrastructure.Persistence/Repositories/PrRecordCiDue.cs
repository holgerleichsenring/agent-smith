using System.Linq.Expressions;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-17-3435b: which of a project's open records the CI loop still waits on, checked before
/// <see cref="CheckedBeforeTicks"/> — a pending (2026-11-23-0219: or not started) CI read since after
/// <see cref="PendingSinceTicks"/>; under <see cref="ActsOnVerdict"/> (2026-11-25-9992i: the project's review is none) also an
/// unread record, one on an older head, or a verdict not acted on. 2026-11-24-629ab: in
/// every mode, an auto-complete lost at a conflict that has ended and was not armed again on this head.
/// <para>2026-11-25-9992e: a record of a ticket a series holds (<see cref="SeriesTickets"/>) is the series master's to read —
/// due at every poll while its head's CI is unread, pending or not started, in every mode; its verdicts nothing acts on.</para>
/// </summary>
public sealed record PrRecordCiDue(long CheckedBeforeTicks, long PendingSinceTicks, bool ActsOnVerdict)
{
    public IReadOnlyCollection<string> SeriesTickets { get; init; } = [];

    public Expression<Func<PrReviewRecord, bool>> Where(string project)
    {
        var (checkedBefore, pendingSince, onVerdict) = (CheckedBeforeTicks, PendingSinceTicks, ActsOnVerdict);
        var series = SeriesTickets;
        return r => r.Project == project && r.State == PrReviewStates.Open && r.CheckedAtTicks < checkedBefore
            && ((series.Contains(r.TicketId) && (r.CiState == null || r.CiHeadSha != r.HeadSha
                    || r.CiState == CiStates.Pending || r.CiState == CiStates.NotStarted))
                || ((r.CiState == CiStates.Pending || r.CiState == CiStates.NotStarted) && r.CiAtTicks > pendingSince)
                || (r.AutoCompleteLostAtTicks != null && r.HasConflicts == false && !r.IsDraft && r.HeadSha != null
                    && (r.AutoCompleteRearmedHead == null || r.AutoCompleteRearmedHead != r.HeadSha))
                || (!series.Contains(r.TicketId) && onVerdict
                    && (r.CiState == null || r.CiHeadSha != r.HeadSha
                        || (r.CiState != CiStates.Pending && r.CiState != CiStates.NotStarted
                            && (r.CiActed == null || r.CiActed != r.CiHeadSha + ":" + r.CiState)))));
    }
}
