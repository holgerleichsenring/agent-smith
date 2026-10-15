using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Checks;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>2026-10-13-9a59h: the checks and hand-backs read for a set of last attempts.</summary>
public sealed record TicketStanding(
    IReadOnlyDictionary<string, TicketBetweenAttemptsState> Checks,
    IReadOnlyDictionary<string, TicketHandbackView> Handbacks,
    IReadOnlyDictionary<(string Project, string TicketId), IReadOnlyList<PrReviewRecordView>> PrRecords)
{
    /// <summary>2026-10-14-dbcei: when each last attempt's ticket landed, merged or by hand; absent when it did not.</summary>
    public IReadOnlyDictionary<(string Project, string TicketId), DateTimeOffset> Landed { get; init; } =
        new Dictionary<(string, string), DateTimeOffset>();

    /// <summary>2026-10-15-114fb: each last attempt's ticket's close mark, in force or not; absent when none is stored.</summary>
    public IReadOnlyDictionary<(string Project, string TicketId), TicketClosedMark> Closed { get; init; } =
        new Dictionary<(string, string), TicketClosedMark>();

    public TicketBetweenAttemptsState? CheckOf(string runId) => Checks.GetValueOrDefault(runId);

    /// <summary>2026-10-15-114fc: the last attempt's own check saw a person close the ticket on the tracker.</summary>
    public bool ClosedForWorkOf(Run last) => CheckOf(last.Id)?.ClosedForWork == true;
    public DateTimeOffset? LandedAt(Run last) => Landed.TryGetValue((last.Project, last.TicketId), out var at) ? at : null;
    public TicketClosedMark? ClosedOf(Run last) => Closed.GetValueOrDefault((last.Project, last.TicketId));

    /// <summary>The full pull-request records of a ticket the check saw unchanged; empty otherwise.</summary>
    public IReadOnlyList<PrReviewRecordView> PrRecordsOf(Run last) => PrRecords.GetValueOrDefault((last.Project, last.TicketId)) ?? [];

    /// <summary>A hand-back row takes its since and its words from the recorded hand-back.</summary>
    public TicketSummaryView Apply(TicketSummaryView summary) =>
        summary.Kind == TicketNeedsYouKind.Handback && Handbacks.GetValueOrDefault(summary.NewestRunId) is { } handback
            ? summary with { Since = handback.At, Handback = handback }
            : summary;
}
