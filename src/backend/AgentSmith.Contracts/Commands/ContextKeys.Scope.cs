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

    /// <summary>p0336b: IReadOnlyDictionary&lt;string, IReadOnlyList&lt;string&gt;&gt; keyed by repo name
    /// → the KEPT context names for that repo, set only when context-level scoping narrowed a
    /// repo below its full context set. A repo ABSENT from this map keeps ALL its contexts
    /// (today's behaviour), so the key is absent entirely when nothing was narrowed. The sandbox
    /// coordinator provisions only the kept contexts; the ensure_repo_sandbox escalation ignores
    /// it (a mid-run escalation to a dropped context is an explicit operator/agent decision).</summary>
    public const string ScopedContexts = "ScopedContexts";

    /// <summary>p0331: human-readable record of the ScopeRepos decision — which repos the
    /// run was narrowed to (or why it kept all of them: low confidence, parse failure, LLM
    /// error, unknown repo name), with the classifier's rationale. Also appended to
    /// ContextKeys.Decisions so result.md and the dashboard surface it; a wrong narrowing
    /// must be diagnosable, never silent.</summary>
    public const string RepoScopeRationale = "RepoScopeRationale";

    /// <summary>p0384: IReadOnlyList&lt;string&gt; of repo names the scope classifier expects
    /// to CHANGE (vs kept only for inspection) — the optional expected_changes subset of
    /// the kept repos, validated by RepoScopeEvaluator. Absent / empty preserves the
    /// keystone's anyCode semantics exactly (fail-open); present, the keystone requires
    /// a committed diff per listed repo.</summary>
    public const string ExpectedChangeRepos = "ExpectedChangeRepos";

    /// <summary>p0413: the <see cref="Models.WorkShapeVerdict"/> the scope classifier
    /// stated for this ticket — the SHAPE of the work (deterministic transformation /
    /// judgement / mixed) with its one-line reason. Read by the spec derivation, which
    /// cuts deterministic work into the fewest phases its deliverable allows. Absent
    /// when no classification ran or the model stated no shape: the derivation is then
    /// told nothing and cuts exactly as it did before.</summary>
    public const string WorkShape = "WorkShape";

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
