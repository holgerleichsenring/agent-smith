namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59e: a list row's series at a glance — whether a cut exists, the newest attempt is
/// still deriving, or it handed back (cut | deriving | handed_back | none), and one cell per spec of the
/// newest cut with specs.
/// </summary>
public sealed record TicketSeriesStripView(string Derive, IReadOnlyList<TicketSeriesCellView> Cells);
