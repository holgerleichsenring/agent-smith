using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// p0356: DB-free default — compositions without a relational store (CLI,
/// spawned orchestrators) read no prior ledger; the master starts on the plan
/// seed exactly as before. The server's relational composition replaces this
/// with the DB-backed reader.
/// </summary>
public sealed class NullPriorRunLedgerReader : IPriorRunLedgerReader
{
    public Task<PriorRunLedger?> ReadLatestForTicketAsync(string ticketId, CancellationToken cancellationToken)
        => Task.FromResult<PriorRunLedger?>(null);
}
