namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>A sandbox spawned for the run: its key, repo, toolchain image and status.</summary>
public sealed class RunSandbox : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? RepoName { get; set; }
    public string? ToolchainImage { get; set; }
    public string? Status { get; set; }

    // p0332: sandbox lifetime, from the event stream's own timestamps
    // (SandboxCreated / SandboxDisposed / SandboxVanished) — NOT the EntityBase
    // audit columns, which record projection write time. Feeds the per-run
    // reserved resource-time (memory request x pod lifetime). Null on pre-p0332 rows.
    public DateTimeOffset? SpawnedAt { get; set; }
    public DateTimeOffset? DisposedAt { get; set; }

    /// <summary>
    /// p0332: the pod's Kubernetes memory-request quantity (e.g. "1Gi") as the
    /// producer declared it on SandboxCreatedEvent. Null when the event predates
    /// the field or the producer didn't carry it — consumers fall back to the
    /// platform default request.
    /// </summary>
    public string? MemoryRequest { get; set; }
}
