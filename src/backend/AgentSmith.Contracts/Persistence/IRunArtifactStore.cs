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
    /// Stores the rendered result.md so the dashboard can read it server-side
    /// (the on-disk write inside the sandbox / target repo is not reachable
    /// from the server). 24h TTL is longer-lived than the other slots —
    /// operators read this AFTER WriteRunResult, not during the pipeline.
    /// PromoteAsync deliberately does NOT clear this slot.
    /// </summary>
    Task WriteResultMarkdownAsync(string runId, string resultMd, CancellationToken cancellationToken);
    Task<string?> ReadResultMarkdownAsync(string runId, CancellationToken cancellationToken);

    /// <summary>
    /// p0235: caches the run's plan.md so the dashboard can show it alongside
    /// result.md. For coding presets the plan is the agent's own
    /// <c>&lt;repo&gt;/.agentsmith/plan.md</c> (read back from the sandbox at
    /// run-finish); for structured presets it is the rendered plan. Same 24h
    /// lifetime as the result slot — survives <see cref="PromoteAsync"/>/
    /// <see cref="ClearAsync"/>, which only drop the transient plan/diff/bootstrap.
    /// </summary>
    Task WritePlanMarkdownAsync(string runId, string planMd, CancellationToken cancellationToken);
    Task<string?> ReadPlanMarkdownAsync(string runId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads all three in-flight slots and returns them; <see cref="ClearAsync"/>
    /// still has to be called explicitly after the caller has durably written
    /// the snapshot. Splitting the read and the clear keeps the store free of
    /// the durability concern (which lives in WriteRunResultHandler). Does
    /// NOT include the result slot — that one has its own lifetime.
    /// </summary>
    Task<RunArtifactSnapshot> PromoteAsync(string runId, CancellationToken cancellationToken);

    Task ClearAsync(string runId, CancellationToken cancellationToken);
}
