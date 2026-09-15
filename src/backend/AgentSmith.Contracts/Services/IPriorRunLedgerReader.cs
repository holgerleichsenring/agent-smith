using AgentSmith.Contracts.Runs;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0356: reads the latest prior run's persisted progress ledger for a ticket —
/// the same-ticket RESUME seed (a reaped/crashed run's mid-run ledger flushes
/// make this real). Distinct from cross-run context ingestion, which stays
/// successful-runs-only: a resume deliberately reads the latest run REGARDLESS
/// of outcome, because the interrupted run's ledger is exactly what a resumed
/// run continues. Returns null when no prior ledger exists or the composition
/// has no database channel (spawned orchestrators, CLI).
/// 2026-09-09-b26f: keyed on PROJECT AND TICKET. A tracker id is unique only
/// within its project, so two projects on one tracker can both carry the same
/// id; on the ticket alone the newer run seeds its checklist from the other
/// project's work and the master opens on a repository it is not working in.
/// </summary>
public interface IPriorRunLedgerReader
{
    Task<PriorRunLedger?> ReadLatestForTicketAsync(
        string project, string ticketId, CancellationToken cancellationToken);
}
