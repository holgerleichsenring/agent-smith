namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-09-17-0e79a: the server's record of what a person approved, keyed by the TRACKER
/// CONNECTION and the spec key. One ticket matching two projects of ONE tracker is spawned twice
/// and is the same work, so one record serves both; two tracker instances numbering a ticket
/// alike are different work, and the spec key alone cannot tell them apart. The per-project
/// pointer stays where it is.
/// <para>
/// It is the FALLBACK route, not the primary one. A funnel run executes in the server and can
/// read it; a chat run is a container and a CLI run is a process, and neither swaps a store in
/// — so the record is CARRIED on the run's initial context, and this store repairs a request
/// nobody built a context for.
/// </para>
/// </summary>
public interface ISpecApprovalStore
{
    Task<SpecApprovalRecord?> GetAsync(string tracker, string key, CancellationToken cancellationToken);

    Task SaveAsync(SpecApprovalRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// 2026-09-25-c1f7: the records of one tracker connection that no run has satisfied yet,
    /// oldest approval first, at most <paramref name="limit"/> of them — what lets a DISCOVERY
    /// query name the tickets an approval still expects work on.
    /// <para>
    /// It enumerates by the stored TICKET ID and not by the spec key, because the key lowercases
    /// the id and replaces every non-alphanumeric character: <c>DPG-1239</c> becomes
    /// <c>jira-dpg-1239</c>, and no query can ask a tracker for that. Recovering the id with a
    /// per-provider parser is what this repository refused to write for the ticket's label stamp,
    /// for the same reason — so the id is a column.
    /// </para>
    /// <para>
    /// SATISFACTION is what bounds it. Without it the set is every ticket this deployment ever
    /// approved, forever, including the ones already done or failed that the parking statuses
    /// exist to exclude.
    /// </para>
    /// </summary>
    Task<OutstandingApprovals> ListOutstandingAsync(
        string tracker, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// 2026-09-25-c1f7: records that a run finished the ticket this record was written for, so a
    /// later discovery query stops naming it. Silent on a record that does not exist — a run
    /// finalizing a ticket nobody approved is the ordinary case, not an error.
    /// </summary>
    Task MarkSatisfiedAsync(
        string tracker, string key, DateTimeOffset at, CancellationToken cancellationToken);
}
