using AgentSmith.Contracts.Models.Configuration;

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
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.AnalyzeCode, CommandNames.Triage,
        CommandNames.GeneratePlan,
        CommandNames.Approval, CommandNames.AgenticExecute,
        CommandNames.Test, CommandNames.WriteRunResult, CommandNames.CommitAndPR,
    ];

    public static readonly IReadOnlyList<string> FixNoTest =
    [
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.BootstrapProject, CommandNames.LoadCodeMap,
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
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
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
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
        CommandNames.LoadCodingPrinciples,
        CommandNames.Triage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.DeliverOutput,
    ];

    public static readonly IReadOnlyList<string> SecurityScan =
    [
        CommandNames.CheckoutSource,
        CommandNames.BootstrapProject,
        CommandNames.LoadContext,             // p0105: project brief from target's .agentsmith/
        CommandNames.LoadCodingPrinciples,
        CommandNames.LoadCodeMap,             // p0105: code-map.yaml for module structure
        CommandNames.StaticPatternScan,
        CommandNames.GitHistoryScan,
        CommandNames.DependencyAudit,
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
        CommandNames.TryCheckoutSource,     // p0102a: fail-soft source resolution (CLI flag, local config, or remote clone)
        CommandNames.LoadContext,           // p0104: target's .agentsmith/context.yaml — soft-fail if absent
        CommandNames.LoadCodingPrinciples,  // p0104: target's .agentsmith/coding-principles.md — soft-fail if absent
        CommandNames.LoadCodeMap,           // p0104: target's .agentsmith/code-map.yaml — soft-fail if absent
        CommandNames.LoadSwagger,
        CommandNames.ApiCodeContext,        // p0102: route → handler mapping when source resolved
        CommandNames.SessionSetup,          // p79: authenticate personas before scan
        CommandNames.SpawnNuclei,
        CommandNames.SpawnSpectral,
        CommandNames.SpawnZap,              // p60: DAST via OWASP ZAP (skips if dast not enabled)
        CommandNames.CompressApiScanFindings, // p67: category slices for skill-specific findings
        CommandNames.CorrelateFindings,     // p0104: deterministic Nuclei/ZAP → handler mapping
        CommandNames.LoadSkills,
        CommandNames.ApiSecurityTriage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileFindings,
        CommandNames.DeliverFindings,
    ];

    public static readonly IReadOnlyList<string> SkillManager =
    [
        CommandNames.DiscoverSkills,
        CommandNames.EvaluateSkills,
        CommandNames.DraftSkillFiles,
        CommandNames.ApproveSkills,
        CommandNames.InstallSkills,
        CommandNames.WriteRunResult,
    ];

    public static readonly IReadOnlyList<string> Autonomous =
    [
        CommandNames.CheckoutSource,
        CommandNames.BootstrapProject,
        CommandNames.LoadContext,
        CommandNames.LoadCodeMap,
        CommandNames.LoadRuns,
        CommandNames.Triage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.WriteTickets,
        CommandNames.WriteRunResult,
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
        ["skill-manager"] = SkillManager,
        ["autonomous"] = Autonomous,
    };

    public static IReadOnlyList<string> Names { get; } = All.Keys.ToList();

    public static IReadOnlyList<string>? TryResolve(string name) =>
        All.GetValueOrDefault(name);

    private static readonly Dictionary<string, PipelineType> PipelineTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fix-bug"] = PipelineType.Hierarchical,
        ["fix-no-test"] = PipelineType.Hierarchical,
        ["add-feature"] = PipelineType.Hierarchical,
        ["init-project"] = PipelineType.Discussion,
        ["security-scan"] = PipelineType.Structured,
        ["api-security-scan"] = PipelineType.Structured,
        ["mad-discussion"] = PipelineType.Discussion,
        ["legal-analysis"] = PipelineType.Discussion,
        ["skill-manager"] = PipelineType.Discussion,
        ["autonomous"] = PipelineType.Discussion,
    };

    /// <summary>
    /// Returns the pipeline interaction type. Defaults to Discussion for unknown pipelines.
    /// </summary>
    public static PipelineType GetPipelineType(string pipelineName) =>
        PipelineTypes.GetValueOrDefault(pipelineName, PipelineType.Discussion);

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
        ["skill-manager"] = "skills/coding",
        ["autonomous"] = "skills/coding",
    };

    /// <summary>
    /// Returns the default skills path for a given pipeline name.
    /// Falls back to "skills/coding" if the pipeline is not mapped.
    /// </summary>
    public static string GetDefaultSkillsPath(string pipelineName) =>
        DefaultSkillsPaths.GetValueOrDefault(pipelineName, "skills/coding");
}
