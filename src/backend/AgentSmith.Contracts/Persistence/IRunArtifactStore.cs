namespace AgentSmith.Contracts.Persistence;

/// <summary>
/// Transient storage for in-flight run artifacts (plan/diff/bootstrap) handed off
/// between pipeline phases. Redis-backed in production with a configurable TTL,
/// in-memory in dev/tests. Filesystem promotion happens at pipeline end via
/// <see cref="PromoteAsync"/>; the caller writes the snapshot to durable storage,
/// then <see cref="ClearAsync"/> drops the transient keys.
/// </summary>
public interface IRunArtifactStore
{
    Task WritePlanAsync(string runId, string planJson, CancellationToken cancellationToken);
    Task<string?> ReadPlanAsync(string runId, CancellationToken cancellationToken);

    Task WriteDiffAsync(string runId, string diffJson, CancellationToken cancellationToken);
    Task<string?> ReadDiffAsync(string runId, CancellationToken cancellationToken);

    Task WriteBootstrapAsync(string runId, string bootstrapMarkdown, CancellationToken cancellationToken);
    Task<string?> ReadBootstrapAsync(string runId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads all three slots and returns them; <see cref="ClearAsync"/> still
    /// has to be called explicitly after the caller has durably written the snapshot.
    /// Splitting the read and the clear keeps the store free of the durability
    /// concern (which lives in WriteRunResultHandler).
    /// </summary>
    Task<RunArtifactSnapshot> PromoteAsync(string runId, CancellationToken cancellationToken);

    Task ClearAsync(string runId, CancellationToken cancellationToken);
}
