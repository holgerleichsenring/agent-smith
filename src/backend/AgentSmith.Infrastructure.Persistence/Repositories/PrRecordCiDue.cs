using System.Linq.Expressions;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-17-3435b: which of a project's open records the CI watch reads next, checked before <see cref="CheckedBeforeTicks"/>.
/// 2026-11-25-96aea: the server's CI loop is gone, so only the tickets a series holds (<see cref="SeriesTickets"/>, every
/// standalone ticket being a series of one) are read for their master — while their head's CI is unread, on an older head,
/// or pending or not started since after <see cref="PendingSinceTicks"/> — twice merge.ci_pending_seconds back, so the read
/// past the deadline that wakes the master once (96aec) still happens, and the polling of a CI that never decides ends.
/// </summary>
public sealed record PrRecordCiDue(long CheckedBeforeTicks, long PendingSinceTicks)
{
    public IReadOnlyCollection<string> SeriesTickets { get; init; } = [];

    public Expression<Func<PrReviewRecord, bool>> Where(string project)
    {
        var (checkedBefore, pendingSince) = (CheckedBeforeTicks, PendingSinceTicks);
        var series = SeriesTickets;
        return r => r.Project == project && r.State == PrReviewStates.Open && r.CheckedAtTicks < checkedBefore
            && series.Contains(r.TicketId)
            && (r.CiState == null || r.CiHeadSha != r.HeadSha
                || ((r.CiState == CiStates.Pending || r.CiState == CiStates.NotStarted) && (r.CiAtTicks == null || r.CiAtTicks > pendingSince)));
    }
}
