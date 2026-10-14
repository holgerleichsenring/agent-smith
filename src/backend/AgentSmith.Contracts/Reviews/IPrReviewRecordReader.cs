namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-13-9a59b: the stored review of a ticket's pull requests — every record the ticket's runs
/// named, with its entries. The read port for everything outside persistence; only the nudge
/// worker's refresh writes the records.
/// </summary>
public interface IPrReviewRecordReader
{
    Task<IReadOnlyList<PrReviewRecordView>> ByTicketAsync(string project, string ticketId, CancellationToken cancellationToken);
}
