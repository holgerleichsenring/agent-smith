namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // Repo-side security scan: three scanners (static-pattern / git-history / dependency-
    // audit), SecurityTrend (p60) for git-based diff analysis, CompressSecurityFindings
    // bundles per-category slices for the security-* skills, then the standard triage +
    // skill-discussion + finalize chain. SecuritySnapshotWrite persists the snapshot for
    // future trend deltas; SpawnFix auto-emits remediation PRs for Critical/High when
    // operator opt-in is enabled.
    // p0179d: collapsed shape. Triage / RunReviewPhase / RunFinalPhase /
    // ConvergenceCheck / CompileDiscussion / CompressSecurityFindings retired
    // FROM THIS PRESET (handlers still alive for skill-manager/autonomous
    // until those migrate). One AgenticMaster step loads the security-master
    // skill via the p0179a adapter and runs the analysis end-to-end over the
    // pattern/history/dependency/trend outputs.
    public static readonly IReadOnlyList<string> SecurityScan =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.CheckoutSource,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadContext,             // p0105: project brief from target's .agentsmith/
        CommandNames.LoadCodingPrinciples,
        CommandNames.StaticPatternScan,
        CommandNames.GitHistoryScan,
        CommandNames.DependencyAudit,
        CommandNames.SecurityTrend,         // p60: git-based trend analysis
        CommandNames.AnalyzeCode,
        CommandNames.AgenticMaster,         // p0179d: loads security-master per pipeline-name routing
        CommandNames.DeliverFindings,
        CommandNames.SecuritySnapshotWrite, // p60: persist snapshot for trend history
        CommandNames.SpawnFix,              // p60: auto-fix for Critical/High (skips if not enabled)
    ];
}
