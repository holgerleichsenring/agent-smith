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
        CommandNames.LoadSkills,
        CommandNames.AnalyzeCode,
        CommandNames.SecurityTriage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.DeliverOutput,
    ];

    public static readonly IReadOnlyList<string> ApiSecurityScan =
    [
        CommandNames.LoadSwagger,
        CommandNames.SpawnNuclei,
        CommandNames.SpawnSpectral,
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
}
