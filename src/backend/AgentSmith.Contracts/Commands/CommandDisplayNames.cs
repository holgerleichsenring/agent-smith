namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0203: central operator-facing display labels for every pipeline command.
/// Surfaces in StepStartedEvent.DisplayName so the dashboard's execution
/// tree shows "Setup private-feed credentials" instead of the C# class
/// name "SetupRegistryAuthCommand". Single source of truth for execution-
/// tree labels — distinct from CommandNames.GetLabel which feeds the
/// Slack/Teams/CLI progress reporter with present-continuous phrases
/// ("Setting up credentials"). The display-name form is the operator's
/// noun-phrase reading of what the step is.
///
/// CommandDisplayNamesCoverageTests reflects against every public const
/// string on CommandNames + its nested partial classes (Pipeline / Api /
/// Security) and fails if a constant has no entry here. Adding a new
/// CommandName without a label is therefore caught by the test suite, not
/// by an operator hitting the dashboard.
/// </summary>
public static class CommandDisplayNames
{
    public static string Get(string commandName)
    {
        if (Labels.TryGetValue(commandName, out var label))
            return label;

        var baseCommand = commandName.Contains(':')
            ? commandName[..commandName.IndexOf(':')]
            : commandName;

        return Labels.TryGetValue(baseCommand, out var baseLabel)
            ? baseLabel
            : commandName;
    }

    public static IReadOnlyDictionary<string, string> All => Labels;

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        [CommandNames.FetchTicket] = "Fetch ticket",
        [CommandNames.CheckoutSource] = "Check out source",
        [CommandNames.TryCheckoutSource] = "Resolve source",
        [CommandNames.SetupRegistryAuth] = "Set up private-feed credentials",
        [CommandNames.EnsurePrerequisites] = "Prepare environment",
        [CommandNames.BootstrapProject] = "Bootstrap project context",
        [CommandNames.LoadCodeMap] = "Load code map",
        [CommandNames.LoadCodingPrinciples] = "Load coding principles",
        [CommandNames.LoadContext] = "Load project context",
        [CommandNames.LoadSkills] = "Load skills",
        [CommandNames.AnalyzeCode] = "Analyze codebase",
        [CommandNames.GeneratePlan] = "Generate plan",
        [CommandNames.EmptyPlanCheck] = "Check plan is non-empty",
        [CommandNames.Approval] = "Await approval",
        [CommandNames.AgenticExecute] = "Execute plan",
        [CommandNames.AgenticMaster] = "Run master skill",
        [CommandNames.WriteRunResult] = "Write run result",
        [CommandNames.CommitAndPR] = "Create pull request",
        [CommandNames.InitCommit] = "Commit init files",
        [CommandNames.PrCrossLink] = "Cross-link sibling pull requests",
        [CommandNames.GenerateTests] = "Generate tests",
        [CommandNames.GenerateDocs] = "Generate docs",
        [CommandNames.Triage] = "Triage ticket",
        [CommandNames.SwitchSkill] = "Switch skill",
        [CommandNames.SkillRound] = "Skill round",
        [CommandNames.ConvergenceCheck] = "Check convergence",
        [CommandNames.CompileDiscussion] = "Compile discussion",
        [CommandNames.AcquireSource] = "Acquire source document",
        [CommandNames.BootstrapDocument] = "Bootstrap document",
        [CommandNames.DeliverOutput] = "Deliver output",
        [CommandNames.SessionSetup] = "Authenticate API personas",
        [CommandNames.Ask] = "Ask human",
        [CommandNames.CompileKnowledge] = "Compile knowledge base",
        [CommandNames.QueryKnowledge] = "Query knowledge base",
        [CommandNames.LoadRuns] = "Load run history",
        [CommandNames.WriteTickets] = "Write tickets",
        [CommandNames.FilterRound] = "Filter round",
        [CommandNames.RunReviewPhase] = "Run review phase",
        [CommandNames.RunFinalPhase] = "Run final phase",
        [CommandNames.PersistWorkBranch] = "Persist work branch",
        [CommandNames.LoadCatalog] = "Load catalog",
        [CommandNames.PipelineNameInitializer] = "Publish pipeline name",
        [CommandNames.BootstrapCheck] = "Check bootstrap files",
        [CommandNames.PlanOpenQuestions] = "Post Plan open questions",
        [CommandNames.RunVerifyPhase] = "Run verify phase",
        [CommandNames.BootstrapGate] = "Verify bootstrap files",
        [CommandNames.PublishProjectLanguage] = "Publish project language",
        [CommandNames.BootstrapDispatch] = "Dispatch bootstrap skill",
        [CommandNames.BootstrapDiscover] = "Discover project components",
        [CommandNames.BootstrapRound] = "Produce bootstrap files",
        [CommandNames.LoadSwagger] = "Load Swagger spec",
        [CommandNames.SpawnNuclei] = "Run Nuclei scan",
        [CommandNames.SpawnSpectral] = "Run Spectral lint",
        [CommandNames.SpawnZap] = "Run ZAP scan",
        [CommandNames.ApiSecuritySkillRound] = "API security skill round",
        [CommandNames.CompileFindings] = "Compile findings",
        [CommandNames.CollectMasterFindings] = "Collect master findings",
        [CommandNames.DeliverFindings] = "Deliver findings",
        [CommandNames.CompressApiScanFindings] = "Compress API scan findings",
        [CommandNames.SecuritySkillRound] = "Security skill round",
        [CommandNames.StaticPatternScan] = "Scan for security patterns",
        [CommandNames.GitHistoryScan] = "Scan git history for secrets",
        [CommandNames.DependencyAudit] = "Audit dependencies",
        [CommandNames.CompressSecurityFindings] = "Compress security findings",
        [CommandNames.MergeMasterFindings] = "Merge master findings",
        [CommandNames.SecurityTrend] = "Analyze security trends",
        [CommandNames.SecuritySnapshotWrite] = "Write security snapshot",
        [CommandNames.SpawnFix] = "Generate security fix requests",
    };
}
