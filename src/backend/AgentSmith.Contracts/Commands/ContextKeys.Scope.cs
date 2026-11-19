namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0331 ticket-scoped provisioning keys: the pre-checkout remote context
/// inventory, the ScopeRepos decision artifact, and the live-coordinator /
/// project handles the ensure_repo_sandbox master tool escalates through.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>p0331: IReadOnlyDictionary&lt;string, IReadOnlyList&lt;RemoteContextDiscovery&gt;&gt;
    /// keyed by repo name — the pre-checkout remote context inventory built by ScopeRepos
    /// (one ResolveAllAsync pass per repo, no sandbox). PipelineSandboxCoordinator consumes
    /// these cached discoveries instead of re-reading every repo's context.yamls remotely.
    /// Covers ALL repos of the run as seen BEFORE narrowing, so a mid-run escalation to an
    /// initially-descoped repo also hits the cache.</summary>
    public const string RemoteContextInventory = "RemoteContextInventory";

    /// <summary>2026-09-08-1830: the <see cref="Models.ScopeNamedContexts"/> the scope call
    /// NAMED for this ticket — its claim, not a narrowing. Set on every ticketed run whose
    /// classification names contexts, a single-repo run included, so the derivation's cut
    /// can be held against it; absent when the scope call named none, or every repository
    /// in scope has a single context. Never read by the sandbox coordinator.</summary>
    public const string ScopeNamedContexts = "ScopeNamedContexts";

    /// <summary>p0413: the <see cref="Models.WorkShapeVerdict"/> the scope classifier
    /// stated for this ticket — the SHAPE of the work (deterministic transformation /
    /// judgement / mixed) with its one-line reason. Read by the spec derivation, which
    /// cuts deterministic work into the fewest specs its deliverable allows. Absent
    /// when no classification ran or the model stated no shape: the derivation is then
    /// told nothing and cuts exactly as it did before.</summary>
    public const string WorkShape = "WorkShape";

    /// <summary>2026-09-22-7c41a: the <see cref="Models.Configuration.ComplexityTier"/> the
    /// scope call estimated, kept beside the cap it sized. The cap itself cannot cross a
    /// park (the checkpoint excludes it, and the scope step that raised it is a completed
    /// command the resume never re-runs), so the resume recomputes it from this — the
    /// configuration as it stands now, raised to this tier. Absent on a run whose tier was
    /// Unknown: that run keeps the configured cap, exactly as before.</summary>
    public const string ComplexityTier = "ComplexityTier";

    /// <summary>p0331: the run's LIVE IPipelineSandboxCoordinator, published by the
    /// coordinator itself on EnsureSandboxesAsync. The instance is transient and OWNED by
    /// PipelineExecutor (`await using`) — the context only BORROWS the reference so the
    /// ensure_repo_sandbox master tool can spawn into the same sandbox set mid-run; a DI
    /// resolve inside a handler would yield a fresh empty coordinator.</summary>
    public const string SandboxCoordinator = "SandboxCoordinator";

    /// <summary>p0331: the run's ResolvedProject, published alongside the coordinator so the
    /// ensure_repo_sandbox tool can validate an escalation target against the PROJECT config
    /// (all configured repos), not just the current — possibly narrowed — ContextKeys.Repos.</summary>
    public const string ProjectConfig = "ProjectConfig";
}
