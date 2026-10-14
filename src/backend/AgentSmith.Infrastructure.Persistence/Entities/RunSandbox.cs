namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>A sandbox spawned for the run: its key, repo, toolchain image and how it ended.</summary>
public sealed class RunSandbox : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? RepoName { get; set; }
    public string? ToolchainImage { get; set; }

    /// <summary>
    /// 2026-10-13-9a59i: how the sandbox ended (SandboxEndReasons) — the first end wins. Null while it
    /// is open, and on rows written before the reason was recorded unless they had vanished.
    /// </summary>
    public string? EndReason { get; set; }

    /// <summary>2026-10-13-9a59i: the exit code and OOM flag a backend read; null without evidence.</summary>
    public int? ExitCode { get; set; }
    public bool? OomKilled { get; set; }

    /// <summary>2026-10-13-9a59i: the create event's trail position — the row's identity, and the
    /// bound a close or the run's end uses to find the launch it belongs to.</summary>
    public long? EventSeq { get; set; }

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

    /// <summary>
    /// p0388a: the pipeline step this sandbox was spawned in. Null on pre-p0388a
    /// rows — unattributed, never guessed.
    /// </summary>
    public int? StepIndex { get; set; }
}
