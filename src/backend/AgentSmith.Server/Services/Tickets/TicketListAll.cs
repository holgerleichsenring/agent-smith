using AgentSmith.Contracts.Reviews;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-14-dbcen: every ticket the list and the graph see, composed by one path — the run-bearing tickets
/// newest first with the inputs their page composition reuses, and the never-run tickets.
/// </summary>
public sealed record TicketListAll(
    IReadOnlyList<(List<TicketRunRow> Rows, TicketSummaryView Summary)> Runs,
    IReadOnlyList<TicketSummaryView> NeverRun,
    TicketLiveState State,
    ILookup<(string Project, string TicketId), PrReviewRecordView> PrRecords,
    TicketStanding Standing,
    bool Truncated);
