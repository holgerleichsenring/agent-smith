namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-c81ed: what a lifecycle act's step reads to re-check its precondition — the Poc row and the revision
/// a rollback names. It writes nothing: only the GitOps commit writes the desired state, after its commit.</summary>
public interface IPocActStore
{
    Task<PocActSubject?> ReadAsync(long pocId, CancellationToken ct);

    Task<PocActRevision?> RevisionAsync(long pocId, int revision, CancellationToken ct);
}
