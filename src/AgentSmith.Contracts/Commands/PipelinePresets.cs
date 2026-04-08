namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Code-defined pipeline presets. YAML pipelines section is an optional override.
/// </summary>
public static class PipelinePresets
{
    public static readonly IReadOnlyList<string> FixBug =
    [
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.BootstrapProject, CommandNames.LoadCodeMap,
        CommandNames.LoadDomainRules, CommandNames.LoadContext,
        CommandNames.AnalyzeCode, CommandNames.Triage,
        CommandNames.GeneratePlan,
        CommandNames.Approval, CommandNames.AgenticExecute,
        CommandNames.Test, CommandNames.WriteRunResult, CommandNames.CommitAndPR,
    ];

    public static readonly IReadOnlyList<string> FixNoTest =
    [
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.BootstrapProject, CommandNames.LoadCodeMap,
        CommandNames.LoadDomainRules, CommandNames.LoadContext,
        CommandNames.AnalyzeCode, CommandNames.Triage,
        CommandNames.GeneratePlan,
        CommandNames.Approval, CommandNames.AgenticExecute,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
    ];

    public static readonly IReadOnlyList<string> InitProject =
    [
        CommandNames.CheckoutSource, CommandNames.BootstrapProject,
        CommandNames.InitCommit,
    ];

    public static readonly IReadOnlyList<string> AddFeature =
    [
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.BootstrapProject, CommandNames.LoadCodeMap,
        CommandNames.LoadDomainRules, CommandNames.LoadContext,
        CommandNames.AnalyzeCode, CommandNames.Triage,
        CommandNames.GeneratePlan, CommandNames.Approval,
        CommandNames.AgenticExecute, CommandNames.GenerateTests,
        CommandNames.Test, CommandNames.GenerateDocs,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
    ];

    public static readonly IReadOnlyList<string> MadDiscussion =
    [
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.BootstrapProject, CommandNames.LoadContext,
        CommandNames.Triage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
    ];

    public static readonly IReadOnlyList<string> LegalAnalysis =
    [
        CommandNames.AcquireSource,
        CommandNames.BootstrapDocument,
        CommandNames.LoadDomainRules,
        CommandNames.Triage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.DeliverOutput,
    ];

    public static readonly IReadOnlyList<string> SecurityScan =
    [
        CommandNames.CheckoutSource,
        CommandNames.BootstrapProject,
        CommandNames.LoadDomainRules,
        CommandNames.StaticPatternScan,
        CommandNames.GitHistoryScan,
        CommandNames.DependencyAudit,
        CommandNames.SpawnZap,              // p60: DAST via OWASP ZAP (skips if dast not enabled)
        CommandNames.SecurityTrend,         // p60: git-based trend analysis
        CommandNames.CompressSecurityFindings,
        CommandNames.LoadSkills,
        CommandNames.AnalyzeCode,
        CommandNames.SecurityTriage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.ExtractFindings,
        CommandNames.DeliverFindings,
        CommandNames.SecuritySnapshotWrite, // p60: persist snapshot for trend history
        CommandNames.SpawnFix,              // p60: auto-fix for Critical/High (skips if not enabled)
    ];

    public static readonly IReadOnlyList<string> ApiSecurityScan =
    [
        CommandNames.LoadSwagger,
        CommandNames.SpawnNuclei,
        CommandNames.SpawnSpectral,
        CommandNames.SpawnZap,              // p60: DAST via OWASP ZAP (skips if dast not enabled)
        CommandNames.LoadSkills,
        CommandNames.ApiSecurityTriage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileFindings,
        CommandNames.DeliverFindings,
    ];

    private static readonly Dictionary<string, IReadOnlyList<string>> All = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fix-bug"] = FixBug,
        ["fix-no-test"] = FixNoTest,
        ["init-project"] = InitProject,
        ["add-feature"] = AddFeature,
        ["mad-discussion"] = MadDiscussion,
        ["legal-analysis"] = LegalAnalysis,
        ["security-scan"] = SecurityScan,
        ["api-security-scan"] = ApiSecurityScan,
    };

    public static IReadOnlyList<string> Names { get; } = All.Keys.ToList();

    public static IReadOnlyList<string>? TryResolve(string name) =>
        All.GetValueOrDefault(name);

    /// <summary>
    /// Default skills directory per pipeline preset.
    /// Used when no explicit skills_path is configured in the project.
    /// </summary>
    private static readonly Dictionary<string, string> DefaultSkillsPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fix-bug"] = "skills/coding",
        ["fix-no-test"] = "skills/coding",
        ["add-feature"] = "skills/coding",
        ["init-project"] = "skills/coding",
        ["security-scan"] = "skills/security",
        ["api-security-scan"] = "skills/api-security",
        ["legal-analysis"] = "skills/legal",
        ["mad-discussion"] = "skills/mad",
    };

    /// <summary>
    /// Returns the default skills path for a given pipeline name.
    /// Falls back to "skills/coding" if the pipeline is not mapped.
    /// </summary>
    public static string GetDefaultSkillsPath(string pipelineName) =>
        DefaultSkillsPaths.GetValueOrDefault(pipelineName, "skills/coding");
}
