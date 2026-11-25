using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-25-9992a: one series as <see cref="SeriesStatusReader"/> read it — the record, its project's reads, its
/// tickets' one state in series order, the page's shape built from that state, and the holds it was read with (null when
/// none were: the merge act).</summary>
public sealed record SeriesRunStatus(
    SeriesRunRecord Record, SeriesRunProjectFacts ProjectFacts, IReadOnlyList<SeriesTicketFact> Facts, SeriesRunTicketsShape Shape,
    IReadOnlyDictionary<string, TicketHold>? Holds);
