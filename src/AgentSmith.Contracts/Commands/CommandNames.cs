namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Single source of truth for all pipeline command names and their display labels.
/// </summary>
public static class CommandNames
{
    public const string FetchTicket = "FetchTicketCommand";
    public const string CheckoutSource = "CheckoutSourceCommand";
    public const string BootstrapProject = "BootstrapProjectCommand";
    public const string LoadCodeMap = "LoadCodeMapCommand";
    public const string LoadCodingPrinciples = "LoadCodingPrinciplesCommand";
    public const string LoadContext = "LoadContextCommand";
    public const string AnalyzeCode = "AnalyzeCodeCommand";
    public const string GeneratePlan = "GeneratePlanCommand";
    public const string Approval = "ApprovalCommand";
    public const string AgenticExecute = "AgenticExecuteCommand";
    public const string Test = "TestCommand";
    public const string WriteRunResult = "WriteRunResultCommand";
    public const string CommitAndPR = "CommitAndPRCommand";
    public const string InitCommit = "InitCommitCommand";
    public const string GenerateTests = "GenerateTestsCommand";
    public const string GenerateDocs = "GenerateDocsCommand";
    public const string Triage = "TriageCommand";
    public const string SwitchSkill = "SwitchSkillCommand";
    public const string SkillRound = "SkillRoundCommand";
    public const string ConvergenceCheck = "ConvergenceCheckCommand";
    public const string CompileDiscussion = "CompileDiscussionCommand";
    public const string AcquireSource = "AcquireSourceCommand";
    public const string BootstrapDocument = "BootstrapDocumentCommand";
    public const string DeliverOutput = "DeliverOutputCommand";
    public const string SecuritySkillRound = "SecuritySkillRoundCommand";
    public const string LoadSwagger = "LoadSwaggerCommand";
    public const string SpawnNuclei = "SpawnNucleiCommand";
    public const string SpawnSpectral = "SpawnSpectralCommand";
    public const string SpawnZap = "SpawnZapCommand";
    public const string ApiSecuritySkillRound = "ApiSecuritySkillRoundCommand";
    public const string CompileFindings = "CompileFindingsCommand";
    public const string LoadSkills = "LoadSkillsCommand";
    public const string DeliverFindings = "DeliverFindingsCommand";
    public const string StaticPatternScan = "StaticPatternScanCommand";
    public const string GitHistoryScan = "GitHistoryScanCommand";
    public const string DependencyAudit = "DependencyAuditCommand";
    public const string CompressSecurityFindings = "CompressSecurityFindingsCommand";
    public const string CompressApiScanFindings = "CompressApiScanFindingsCommand";
    public const string SecurityTrend = "SecurityTrendCommand";
    public const string SecuritySnapshotWrite = "SecuritySnapshotWriteCommand";
    public const string Ask = "AskCommand";
    public const string SpawnFix = "SpawnFixCommand";
    public const string DiscoverSkills = "DiscoverSkillsCommand";
    public const string EvaluateSkills = "EvaluateSkillsCommand";
    public const string DraftSkillFiles = "DraftSkillFilesCommand";
    public const string ApproveSkills = "ApproveSkillsCommand";
    public const string InstallSkills = "InstallSkillsCommand";
    public const string CompileKnowledge = "CompileKnowledgeCommand";
    public const string QueryKnowledge = "QueryKnowledgeCommand";
    public const string LoadRuns = "LoadRunsCommand";
    public const string WriteTickets = "WriteTicketsCommand";
    public const string SessionSetup = "SessionSetupCommand";
    public const string ApiCodeContext = "ApiCodeContextCommand";
    public const string TryCheckoutSource = "TryCheckoutSourceCommand";
    public const string CorrelateFindings = "CorrelateFindingsCommand";

    // p0111c: phase-based triage
    public const string FilterRound = "FilterRoundCommand";
    public const string RunReviewPhase = "RunReviewPhaseCommand";
    public const string RunFinalPhase = "RunFinalPhaseCommand";

    // p0112: branch persistence — pipeline-failure recovery commit + push
    public const string PersistWorkBranch = "PersistWorkBranchCommand";

    // p0125c: typed concept publication
    public const string PipelineNameInitializer = "PipelineNameInitializerCommand";
    public const string BootstrapCheck = "BootstrapCheckCommand";

    // p0128b: Plan open_questions round-trip
    public const string PlanOpenQuestions = "PlanOpenQuestionsCommand";

    // p0129a: Verify phase between Implementation and delivery
    public const string RunVerifyPhase = "RunVerifyPhaseCommand";

    // p0130a: bootstrap-files gate (aborts when context.yaml or coding-principles.md is missing)
    public const string BootstrapGate = "BootstrapGateCommand";

    // p0130c: project-language publication + bootstrap-skill dispatch (init-project)
    public const string PublishProjectLanguage = "PublishProjectLanguageCommand";
    public const string BootstrapDispatch = "BootstrapDispatchCommand";

    // p0130c-followup: producer-loop runtime for bootstrap skills (csharp/node/python/
    // generic-bootstrap). Distinct from SkillRound because the producer needs a
    // tool-bearing chat call (WriteFile to emit context.yaml + coding-principles.md),
    // not the observation-only discussion path SkillRoundHandlerBase ran it through.
    public const string BootstrapRound = "BootstrapRoundCommand";

    public static string GetLabel(string commandName)
    {
        if (Labels.TryGetValue(commandName, out var label))
            return label;

        // Handle parameterized commands (e.g. "SkillRoundCommand:architect:1")
        var baseCommand = commandName.Contains(':')
            ? commandName[..commandName.IndexOf(':')]
            : commandName;

        return Labels.GetValueOrDefault(baseCommand, commandName);
    }

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        [FetchTicket] = "Fetching ticket",
        [CheckoutSource] = "Checking out source",
        [BootstrapProject] = "Bootstrapping project context",
        [LoadCodeMap] = "Loading code map",
        [LoadCodingPrinciples] = "Loading coding principles",
        [LoadContext] = "Loading project context",
        [AnalyzeCode] = "Analyzing codebase",
        [GeneratePlan] = "Generating plan",
        [Approval] = "Awaiting approval",
        [AgenticExecute] = "Executing plan",
        [Test] = "Running tests",
        [WriteRunResult] = "Writing run result",
        [CommitAndPR] = "Creating pull request",
        [InitCommit] = "Committing init files",
        [GenerateTests] = "Generating tests",
        [GenerateDocs] = "Generating docs",
        [Triage] = "Triaging ticket",
        [SwitchSkill] = "Switching skill",
        [SkillRound] = "Skill round",
        [ConvergenceCheck] = "Checking convergence",
        [CompileDiscussion] = "Compiling discussion",
        [AcquireSource] = "Acquiring source document",
        [BootstrapDocument] = "Bootstrapping document",
        [DeliverOutput] = "Delivering output",
        [SecuritySkillRound] = "Security skill round",
        [LoadSwagger] = "Loading swagger spec",
        [SpawnNuclei] = "Running Nuclei scan",
        [SpawnSpectral] = "Running Spectral lint",
        [SpawnZap] = "Running ZAP scan",
        [ApiSecuritySkillRound] = "API security skill round",
        [CompileFindings] = "Compiling findings",
        [LoadSkills] = "Loading skills",
        [DeliverFindings] = "Delivering findings",
        [StaticPatternScan] = "Scanning for security patterns",
        [GitHistoryScan] = "Scanning git history for secrets",
        [DependencyAudit] = "Auditing dependencies",
        [CompressSecurityFindings] = "Compressing security findings",
        [CompressApiScanFindings] = "Compressing API scan findings",
        [SecurityTrend] = "Analyzing security trends",
        [SecuritySnapshotWrite] = "Writing security snapshot",
        [Ask] = "Asking human",
        [SpawnFix] = "Generating security fix requests",
        [DiscoverSkills] = "Discovering skill candidates",
        [EvaluateSkills] = "Evaluating skill candidates",
        [DraftSkillFiles] = "Drafting skill files",
        [ApproveSkills] = "Awaiting skill approval",
        [InstallSkills] = "Installing approved skills",
        [CompileKnowledge] = "Compiling knowledge base",
        [QueryKnowledge] = "Querying knowledge base",
        [LoadRuns] = "Loading run history",
        [WriteTickets] = "Writing tickets",
        [SessionSetup] = "Authenticating API personas",
        [ApiCodeContext] = "Mapping API routes to source",
        [TryCheckoutSource] = "Resolving source",
        [CorrelateFindings] = "Correlating findings to handlers",
        [FilterRound] = "Filter round",
        [RunReviewPhase] = "Running review phase",
        [RunFinalPhase] = "Running final phase",
        [PersistWorkBranch] = "Persisting work branch",
        [PipelineNameInitializer] = "Publishing pipeline name",
        [BootstrapCheck] = "Checking bootstrap files",
        [PlanOpenQuestions] = "Posting Plan open questions",
        [RunVerifyPhase] = "Running verify phase",
        [BootstrapGate] = "Verifying bootstrap files",
        [PublishProjectLanguage] = "Publishing project language",
        [BootstrapDispatch] = "Dispatching bootstrap skill",
        [BootstrapRound] = "Producing bootstrap files",
    };

    /// <summary>
    /// Commands that have been retired. Maps the old command name to a migration
    /// hint shown to operators with custom pipeline presets that still reference it.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RetiredCommands =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ExtractFindings"] = "ExtractFindings was retired in p0123. Observations are now produced directly by scanners and gates. Remove the step from your pipeline preset; see .agentsmith/decisions.md p0123 for context.",
            ["BootstrapProjectCommand"] = "BootstrapProject was retired in p0131b. The init-project pipeline now produces .agentsmith/context.yaml + coding-principles.md via a SkillRound dispatch (csharp/node/python/generic-bootstrap); other pipelines should rely on the BootstrapGate added in p0130a to fail-fast on missing files. Remove the step from your custom preset.",
            ["LoadCodeMapCommand"] = "LoadCodeMap was retired in p0131b together with the code-map.yaml artifact. ProjectMap (populated by AnalyzeCode) is now the single source of truth for module/test-project structure; ContextKeys.CodeMap is still emitted as a free-form text rendering for prompt-builders. Remove the step from your custom preset.",
        };

    /// <summary>
    /// Returns a migration hint if the given command name was retired in a past phase.
    /// </summary>
    public static bool TryGetRetirementMessage(string commandName, out string message)
    {
        if (RetiredCommands.TryGetValue(commandName, out var found))
        {
            message = found;
            return true;
        }
        message = "";
        return false;
    }
}
