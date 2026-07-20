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
/// </summary>
public interface IPriorRunLedgerReader
{
    Task<PriorRunLedger?> ReadLatestForTicketAsync(string ticketId, CancellationToken cancellationToken);
}
