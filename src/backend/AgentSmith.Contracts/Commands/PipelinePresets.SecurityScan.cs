namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // Repo-side security scan: three scanners (static-pattern / git-history / dependency-
    // audit), SecurityTrend (p60) for git-based diff analysis, CompressSecurityFindings
    // bundles per-category slices for the security-* skills, then the standard triage +
    // skill-discussion + finalize chain. SecuritySnapshotWrite persists the snapshot for
    // future trend deltas; SpawnFix auto-emits remediation PRs for Critical/High when
    // operator opt-in is enabled.
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
        CommandNames.CompressSecurityFindings,
        CommandNames.LoadSkills,
        CommandNames.AnalyzeCode,
        CommandNames.Triage,                // p0111c: unified triage replaces SecurityTriage
        CommandNames.RunReviewPhase,
        CommandNames.RunFinalPhase,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.DeliverFindings,
        CommandNames.SecuritySnapshotWrite, // p60: persist snapshot for trend history
        CommandNames.SpawnFix,              // p60: auto-fix for Critical/High (skips if not enabled)
    ];
}
