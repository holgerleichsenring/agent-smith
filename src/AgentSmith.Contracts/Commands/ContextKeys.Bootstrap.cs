namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Bootstrap + skill-manager + autonomous PipelineContext keys. Covers the
/// skill-manager workflow (candidates → evaluations → approval → install),
/// init-project mode flag, autonomous-pipeline findings + written-tickets,
/// query-knowledge answer, and the run-history list (consumed by autonomous-*).
/// </summary>
public static partial class ContextKeys
{
    public const string InitMode = "InitMode";

    /// <summary>p0161d: dictionary keyed by repo name holding the read-only
    /// BootstrapDiscover output — the list of independently-deployable /
    /// callable components found in that repo, with workdir + language +
    /// evidence per entry. Published by BootstrapDiscoverHandler; read by
    /// BootstrapDispatchHandler to fan out one BootstrapRound per (repo,
    /// component). Absent on re-init when SandboxDiscoveries already
    /// surfaces non-synthetic contexts.</summary>
    public const string DiscoveredComponents = "DiscoveredComponents";

    /// <summary>p0161d: set by BootstrapDiscoverHandler when the LLM marked
    /// the discovery output as ambiguous (multiple candidates can't be
    /// distinguished without operator input). BootstrapDispatchHandler
    /// refuses to emit any round and fails the pipeline with the structured
    /// message so the operator re-runs via CLI (interactive transport gives
    /// ask_human a working path).</summary>
    public const string DiscoveryAmbiguous = "DiscoveryAmbiguous";

    /// <summary>p0161d: free-form <c>applies_to:</c> value from the active phase
    /// YAML. When set, AgentPromptBuilder and BootstrapPromptFactory render an
    /// "Applies to: ..." prefix line so the LLM knows which stack(s) the work
    /// targets. Optional — falls back silently to the existing per-context
    /// language annotation when absent (p0161a D4 fallback chain).</summary>
    public const string PhaseAppliesTo = "PhaseAppliesTo";

    /// <summary>p0161d: per-(repo, context) bootstrap-skill markdown summaries
    /// accumulated by BootstrapRoundHandler across all rounds. Dictionary
    /// shape: <c>repoName → contextName → markdownSummary</c>. Read by
    /// WriteRunResultHandler's init-mode fan-out to render per-repo
    /// result.md with one section per discovered component. Distinct from
    /// the legacy <c>SkillOutputs</c> dict (keyed by skill name, prone to
    /// per-component collisions when multiple components share a language).</summary>
    public const string BootstrapOutputs = "BootstrapOutputs";

    public const string SkillCandidates = "SkillCandidates";
    public const string SkillEvaluations = "SkillEvaluations";
    public const string SkillInstallPath = "SkillInstallPath";
    public const string ApprovedSkills = "ApprovedSkills";

    public const string WikiUpdates = "WikiUpdates";
    public const string QueryAnswer = "QueryAnswer";
    public const string RunHistory = "RunHistory";
    public const string AutonomousFindings = "AutonomousFindings";
    public const string WrittenTickets = "WrittenTickets";
}
