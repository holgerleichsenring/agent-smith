using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Code-defined pipeline presets. YAML pipelines section is an optional override.
/// </summary>
public static class PipelinePresets
{
    // p0125c: PipelineNameInitializer is prepended to every preset so pipeline_name
    // is published once before any other handler runs. The Initializer reads
    // ResolvedPipeline from PipelineContext (populated by PipelineConfigResolver).
    // p0130a: BootstrapCheck + BootstrapGate are inserted directly after the
    // source-checkout step in code-touching pipelines so missing context.yaml
    // / coding-principles.md aborts with a clear "run init-project first"
    // message before the Load* steps fail in less-actionable ways.
    public static readonly IReadOnlyList<string> FixBug =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.LoadSkills, // p0137a: AvailableRoles for StructuredTriageStrategy
        CommandNames.AnalyzeCode, CommandNames.Triage,
        CommandNames.GeneratePlan, CommandNames.PlanOpenQuestions,
        CommandNames.EmptyPlanCheck, // p0140e: skip Apply/Verify/Commit if Plan has zero steps
        CommandNames.Approval, CommandNames.AgenticExecute,
        CommandNames.RunReviewPhase, CommandNames.RunFinalPhase,
        CommandNames.RunVerifyPhase, // p0129a
        CommandNames.Test, CommandNames.WriteRunResult, CommandNames.CommitAndPR,
    ];

    public static readonly IReadOnlyList<string> FixNoTest =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.LoadSkills, // p0137a: AvailableRoles for StructuredTriageStrategy
        CommandNames.AnalyzeCode, CommandNames.Triage,
        CommandNames.GeneratePlan, CommandNames.PlanOpenQuestions,
        CommandNames.EmptyPlanCheck, // p0140e
        CommandNames.Approval, CommandNames.AgenticExecute,
        CommandNames.RunReviewPhase, CommandNames.RunFinalPhase,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
    ];

    // p0130c: InitProject migrates from BootstrapProjectHandler to SkillRound dispatch.
    // AnalyzeCode populates ProjectMap; PublishProjectLanguage maps PrimaryLanguage to
    // the typed project_language enum; LoadSkills loads the bootstrap-* producers from
    // skills/coding/; BootstrapDispatch deterministically emits exactly one SkillRound
    // for the matching skill (csharp/node/python/generic-bootstrap). The skill writes
    // .agentsmith/context.yaml + coding-principles.md via WriteFile (path-write-guard
    // restricts writes to those two paths); InitCommit then commits the new files.
    public static readonly IReadOnlyList<string> InitProject =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.CheckoutSource,
        CommandNames.AnalyzeCode,                // populates ProjectMap
        CommandNames.PublishProjectLanguage,     // p0130c: ProjectMap.PrimaryLanguage → project_language enum
        CommandNames.LoadSkills,                 // populates AvailableRoles
        CommandNames.BootstrapDispatch,          // p0130c: emits SkillRound for the matching bootstrap skill
        CommandNames.WriteRunResult,
        CommandNames.InitCommit,
    ];

    public static readonly IReadOnlyList<string> AddFeature =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.LoadSkills, // p0137a: AvailableRoles for StructuredTriageStrategy
        CommandNames.AnalyzeCode, CommandNames.Triage,
        CommandNames.GeneratePlan, CommandNames.PlanOpenQuestions,
        CommandNames.EmptyPlanCheck, // p0140e
        CommandNames.Approval,
        CommandNames.AgenticExecute, CommandNames.GenerateTests,
        CommandNames.RunReviewPhase, CommandNames.RunFinalPhase,
        CommandNames.RunVerifyPhase, // p0129a
        CommandNames.Test, CommandNames.GenerateDocs,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
    ];

    public static readonly IReadOnlyList<string> MadDiscussion =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.LoadContext,
        CommandNames.LoadSkills, // p0137a: AvailableRoles for StructuredTriageStrategy
        CommandNames.Triage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
    ];

    public static readonly IReadOnlyList<string> LegalAnalysis =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.AcquireSource,
        CommandNames.BootstrapDocument,
        CommandNames.LoadCodingPrinciples,
        CommandNames.LoadSkills, // p0137a: AvailableRoles for StructuredTriageStrategy
        CommandNames.Triage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.DeliverOutput,
    ];

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

    public static readonly IReadOnlyList<string> ApiSecurityScan =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.TryCheckoutSource,     // p0102a: fail-soft source resolution (CLI flag, local config, or remote clone)
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a conditional gate (skips when source_available=false)
        CommandNames.LoadContext,           // p0104: target's .agentsmith/context.yaml — soft-fail if absent
        CommandNames.LoadCodingPrinciples,  // p0104: target's .agentsmith/coding-principles.md — soft-fail if absent
        CommandNames.LoadSwagger,
        CommandNames.SessionSetup,          // p79: authenticate personas before scan
        CommandNames.SpawnNuclei,
        CommandNames.SpawnSpectral,
        CommandNames.SpawnZap,              // p60: DAST via OWASP ZAP (skips if dast not enabled)
        CommandNames.CompressApiScanFindings, // p67: category slices for skill-specific findings
        CommandNames.LoadSkills,
        CommandNames.Triage,                // p0111c: unified triage replaces ApiSecurityTriage
        CommandNames.RunReviewPhase,
        CommandNames.RunFinalPhase,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileFindings,
        CommandNames.DeliverFindings,
    ];

    // p0144: standard Discussion-pipeline shape — Triage picks the skill-manager-*
    // skills (planner, investigator, judge, filter) deterministically (p0143), the
    // proposed SKILL.md goes through GeneratePlan/Approve before AgenticExecute
    // writes it via WriteFile gated by Bootstrap-phase ToolKit (writes restricted
    // to the .agentsmith/ subtree). Pre-p0144's bespoke DiscoverSkills/Evaluate/
    // Approve/Install chain retired — see RetiredCommands for migration hints.
    public static readonly IReadOnlyList<string> SkillManager =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.LoadSkills,
        CommandNames.LoadContext,
        CommandNames.Triage,
        CommandNames.SkillRound,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.GeneratePlan,
        CommandNames.Approval,
        CommandNames.AgenticExecute,
        CommandNames.WriteRunResult,
    ];

    // p0144: standard Discussion-pipeline shape with autonomous-* skills (planner,
    // investigator, judge, filter). Adds SkillRound between Triage and Convergence
    // (pre-p0144 the preset had Triage but no SkillRound — crashed since p0131c).
    public static readonly IReadOnlyList<string> Autonomous =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.CheckoutSource,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadContext,
        CommandNames.LoadRuns,
        CommandNames.LoadSkills,
        CommandNames.Triage,
        CommandNames.SkillRound,
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
    /// p0131c-pre: true when the named preset emits a single Plan-phase batch
    /// (no <see cref="CommandNames.RunReviewPhase"/> / <see cref="CommandNames.RunFinalPhase"/>
    /// steps). Drives <c>StructuredTriageStrategy</c>'s phase-collapse logic so
    /// LLM-emitted Review-/Final-phase skill assignments don't get silently
    /// dropped on presets that don't run those phases.
    /// Unknown pipeline names default to <c>true</c> (single-phase) — safer
    /// because emitting Review/Final commands for an unknown preset would
    /// dispatch to handlers that may not be in the run.
    /// </summary>
    public static bool IsSinglePhase(string pipelineName)
    {
        var preset = TryResolve(pipelineName);
        if (preset is null) return true;
        return !preset.Contains(CommandNames.RunReviewPhase, StringComparer.Ordinal)
            && !preset.Contains(CommandNames.RunFinalPhase, StringComparer.Ordinal);
    }

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

    /// <summary>
    /// Maps a pipeline name to the SkillRound-family command its handlers expect.
    /// security-scan → SecuritySkillRoundCommand, api-security-scan → ApiSecuritySkillRoundCommand,
    /// everything else → SkillRoundCommand. Filter assignments always emit FilterRoundCommand.
    /// </summary>
    public static string GetSkillRoundCommandName(string pipelineName) => pipelineName.ToLowerInvariant() switch
    {
        "security-scan" => CommandNames.SecuritySkillRound,
        "api-security-scan" => CommandNames.ApiSecuritySkillRound,
        _ => CommandNames.SkillRound
    };
}
