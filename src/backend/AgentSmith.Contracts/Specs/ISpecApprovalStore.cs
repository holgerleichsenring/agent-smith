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
}
